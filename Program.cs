using System.Text;
using System.Net.Http.Headers;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Security;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ========================
// CONFIG PAYWAY (UserSecrets / Render Env)
// ========================
builder.Services.Configure<PaywayOptions>(
    builder.Configuration.GetSection("Payway")
);

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<PaywayOptions>>().Value
);

// ========================
// DATABASE
// ========================
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ========================
// HTTP CLIENT PAYWAY (URL REAL)
// ========================
builder.Services.AddHttpClient<IPaywayService, PaywayService>((sp, client) =>
{
    client.BaseAddress = new Uri("https://developers.decidir.com");
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json")
    );
});

// ========================
// JWT
// ========================
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new Exception("Jwt:Key missing");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new Exception("Jwt:Issuer missing");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new Exception("Jwt:Audience missing");

builder.Services
.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(opt =>
{
    opt.RequireHttpsMetadata = true;
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)
        )
    };
});

// ========================
// SERVICES
// ========================
builder.Services.AddScoped<IPaywayService, PaywayService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

// ========================
// CONTROLLERS + JSON
// ========================
builder.Services.AddControllers()
.AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.PropertyNamingPolicy = null;
    opt.JsonSerializerOptions.WriteIndented = true;
});

// ========================
// CORS (EL PROBLEMA REAL)
// ========================
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000",
                "https://forrajeria-jovita.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ========================
// SWAGGER
// ========================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ForrajeriaJovitaAPI",
        Version = "v1"
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Bearer {token}"
    };

    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

// ========================
// MIDDLEWARE ORDER (IMPORTANTE)
// ========================
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseRouting();

// 👉 CORS ANTES DE AUTH
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ========================
// HEALTH CHECK
// ========================
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "OK",
    service = "Forrajeria Jovita API",
    time = DateTime.UtcNow
}));

// ========================
// STARTUP LOG
// ========================
var payway = app.Services.GetRequiredService<PaywayOptions>();
app.Logger.LogInformation("🚀 API iniciada");
app.Logger.LogInformation("💳 Payway Keys | Public:{Pub} Private:{Priv} Site:{Site}",
    !string.IsNullOrEmpty(payway.PublicKey),
    !string.IsNullOrEmpty(payway.PrivateKey),
    !string.IsNullOrEmpty(payway.SiteId)
);

app.Run();
