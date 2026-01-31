using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Security;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// -----------------------
// Logging (útil para Render / prod)
// -----------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// =====================================================
// DB
// =====================================================
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// =====================================================
// PAYWAY
// =====================================================
builder.Services.Configure<PaywayOptions>(builder.Configuration.GetSection("Payway"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PaywayOptions>>().Value);

builder.Services.AddHttpClient<IPaywayService, PaywayService>((sp, client) =>
{
    var cfg = sp.GetRequiredService<PaywayOptions>();
    if (string.IsNullOrWhiteSpace(cfg.ApiUrl))
        throw new Exception("Payway ApiUrl not configured");

    client.BaseAddress = new Uri(cfg.ApiUrl.EndsWith("/") ? cfg.ApiUrl : cfg.ApiUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

// =====================================================
// JWT
// =====================================================
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("Jwt:Key missing");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new Exception("Jwt:Issuer missing");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new Exception("Jwt:Audience missing");

builder.Services.AddSingleton(new JwtSettings
{
    Key = jwtKey,
    Issuer = jwtIssuer,
    Audience = jwtAudience
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// =====================================================
// DEPENDENCY INJECTION (agregá/ajustá según tus implementaciones)
// =====================================================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IStockService, StockService>();

// Si tenés implementaciones, registralas. Si no, registra stubs temporales.
// Asegurate de que ClientAccountService y CategoryService existan y compilen.
builder.Services.AddScoped<IClientAccountService, ClientAccountService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// =====================================================
// CONTROLLERS / JSON
// =====================================================
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = null;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// =====================================================
// SWAGGER
// =====================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ForrajeriaJovitaAPI", Version = "v1" });
});

// =====================================================
// CORS (policy por defecto y policy "dev" para pruebas)
// =====================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://forrajeria-jovita.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    // Policy temporal para DEV: permite cualquier origen (no usar en prod si necesitás credenciales)
    options.AddPolicy("DevAllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// =====================================================
// FORWARDED HEADERS (Renders / proxies)
// =====================================================
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// =====================================================
// BUILD
// =====================================================
var app = builder.Build();

// -----------------------
// Manejo global de errores (muestra stack en Development)
// -----------------------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errApp =>
    {
        errApp.Run(async context =>
        {
            var exFeature = context.Features.Get<IExceptionHandlerFeature>();
            var ex = exFeature?.Error;
            app.Logger.LogError(ex, "Unhandled exception");

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var payload = new
            {
                error = "Internal Server Error",
                detail = app.Environment.IsDevelopment() ? ex?.ToString() : null
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        });
    });
}

// -----------------------
// =====================================================
// PIPELINE (orden importante)
// =====================================================
app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection(); // comentado porque Render maneja HTTPS

app.UseForwardedHeaders();

app.UseRouting();

// Usa la policy DevAllowAll en Development para descartar problemas de origen.
// En producción usa "AllowFrontend".
app.UseCors(app.Environment.IsDevelopment() ? "DevAllowAll" : "AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// =====================================================
// STARTUP LOG (info útil)
// =====================================================
try
{
    var payway = app.Services.GetRequiredService<PaywayOptions>();
    app.Logger.LogInformation("Payway ApiUrl: {Url}", payway.ApiUrl);
}
catch { }

app.Run();
