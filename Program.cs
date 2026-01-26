// Program.cs
using System;
using System.Net.Http.Headers;
using System.Text;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// Bind Payway options (config/appsettings or Secrets)
// -------------------------
builder.Services.Configure<PaywayOptions>(builder.Configuration.GetSection("Payway"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PaywayOptions>>().Value);

// -------------------------
// DB Context
// -------------------------
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(defaultConn))
{
    throw new Exception("DefaultConnection not configured");
}

builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(defaultConn));

// -------------------------
// Normalize Payway ApiUrl and register HttpClient
// -------------------------
builder.Services.AddSingleton<Func<string>>(() =>
{
    var cfg = builder.Configuration.GetSection("Payway").Get<PaywayOptions>() ?? new PaywayOptions();
    var raw = cfg.ApiUrl?.Trim() ?? string.Empty;

    if (string.IsNullOrEmpty(raw))
        throw new Exception("Payway:ApiUrl is not configured");

    // Ensure scheme (http/https)
    if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
        !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        raw = "https://" + raw;
    }

    // Remove trailing slash
    raw = raw.TrimEnd('/');

    return raw;
});

builder.Services.AddHttpClient<IPaywayService, PaywayService>((sp, client) =>
{
    var normalizeApiUrl = sp.GetRequiredService<Func<string>>()();
    client.BaseAddress = new Uri(normalizeApiUrl);
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ForrajeriaJovitaAPI/1.0");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
});

// -------------------------
// Authentication (JWT) - requiere configurar Jwt:Key, Jwt:Issuer, Jwt:Audience
// -------------------------
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("Jwt:Key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new Exception("Jwt:Issuer not configured");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new Exception("Jwt:Audience not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// -------------------------
// Register app services (agregar otros si los necesitás)
// -------------------------
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
// Nota: IPaywayService ya queda registrado por AddHttpClient<IPaywayService, PaywayService>

// -------------------------
// MVC / Swagger
// -------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
        o.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ForrajeriaJovitaAPI", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token with 'Bearer ' prefix",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = JwtBearerDefaults.AuthenticationScheme }
    };

    c.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, Array.Empty<string>() } });
});

// -------------------------
// CORS - permite frontends específicos y credenciales si es necesario
// -------------------------
var allowedOrigins = new[]
{
    "http://localhost:5173",
    "http://localhost:3000",
    "https://forrajeria-jovita.vercel.app"
};

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // si tu frontend envía cookies/credentials
    });
});

var app = builder.Build();

// -------------------------
// Pipeline
// -------------------------
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ForrajeriaJovitaAPI v1"));

app.UseHttpsRedirection();
app.UseRouting();

// IMPORTANT: Cors debe estar entre UseRouting y UseAuthorization
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// -------------------------
// Startup logs
// -------------------------
var paywayOptions = app.Services.GetRequiredService<PaywayOptions>();
app.Logger.LogInformation("💳 Payway ApiUrl: {Url}", paywayOptions.ApiUrl);
app.Logger.LogInformation(
    "🔑 Keys present: Public={Pub} Private={Priv} Site={Site}",
    !string.IsNullOrEmpty(paywayOptions.PublicKey),
    !string.IsNullOrEmpty(paywayOptions.PrivateKey),
    !string.IsNullOrEmpty(paywayOptions.SiteId)
);

app.Run();
