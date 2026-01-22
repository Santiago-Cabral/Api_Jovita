using System;
using System.Net.Security;
using System.Text;
using System.Text.Json.Serialization;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Security;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// ==============================================================================
// 0. PAYWAY OPTIONS CLASS BINDING (needs PaywayOptions class present in project)
// ==============================================================================
builder.Services.Configure<PaywayOptions>(builder.Configuration.GetSection("Payway"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PaywayOptions>>().Value);

// ==============================================================================
// 1. LOGGING & DATABASE
// ==============================================================================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ==============================================================================
// 2. POLICIES (Polly) - Retry Policy
// ==============================================================================
static IAsyncPolicy<System.Net.Http.HttpResponseMessage> GetRetryPolicy()
{
    // Retry on transient errors (5xx, network issues) with exponential backoff
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

// ==============================================================================
// 3. PAYWAY HTTP CLIENT CONFIGURATION (HttpClientFactory + Polly)
// ==============================================================================
builder.Services.AddHttpClient<IPaywayService, PaywayService>((sp, client) =>
{
    var cfg = sp.GetRequiredService<ForrajeriaJovitaAPI.PaywayOptions?>();
    if (cfg == null) cfg = sp.GetRequiredService<IConfiguration>().GetSection("Payway").Get<PaywayOptions>();

    var env = (cfg?.Environment ?? builder.Configuration["Payway:Environment"] ?? "sandbox").ToLowerInvariant();

    var baseUrl = env == "production"
        ? (cfg?.LiveApiBaseUrl ?? builder.Configuration["Payway:LiveApiBaseUrl"] ?? "https://live.decidir.com/api/v2")
        : (cfg?.SandboxApiBaseUrl ?? builder.Configuration["Payway:SandboxApiBaseUrl"] ?? "https://developers.decidir.com/api/v2");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.Add("User-Agent", "ForrajeriaJovitaAPI/1.0");
    client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
})
.AddPolicyHandler(GetRetryPolicy())
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new System.Net.Http.HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3
    };

    var env = builder.Configuration["Payway:Environment"]?.ToLower();
    var isProduction = env == "production" || builder.Environment.IsProduction();

    if (isProduction)
    {
        // En producción validación estricta de certificados
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            errors == SslPolicyErrors.None;
    }
    else
    {
        // En sandbox/dev: aceptar certificados (útil para entornos de pruebas).
        // NO dejar esto en producción.
        handler.ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    return handler;
});

// ==============================================================================
// 4. AUTHENTICATION & JWT
// ==============================================================================
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("Jwt:Key is not configured in appsettings.json");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new Exception("Jwt:Issuer is not configured");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new Exception("Jwt:Audience is not configured");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Requerir HTTPS en producción
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
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
            IssuerSigningKey = signingKey
        };
    });

// ==============================================================================
// 5. DEPENDENCY INJECTION (SERVICES)
// ==============================================================================
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IClientAccountService, ClientAccountService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();

// NOTA: IPaywayService ya está registrado con AddHttpClient arriba.

// ==============================================================================
// 6. CONTROLLERS & SWAGGER
// ==============================================================================
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.PropertyNamingPolicy = null; // Mantener PascalCase en output
        o.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ForrajeriaJovitaAPI",
        Version = "v1",
        Description = "API for Forrajería Jovita - Sales & Payment System"
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token with 'Bearer ' prefix (e.g., 'Bearer eyJ...')",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    c.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

// ==============================================================================
// 7. CORS CONFIGURATION
// ==============================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000",
                "https://forrajeria-jovita.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// ==============================================================================
// 8. MIDDLEWARE PIPELINE
// ==============================================================================
if (app.Environment.IsDevelopment())
{
    // Dev only: swagger UI enabled
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ForrajeriaJovitaAPI v1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    // Opcional: solo mostrar swagger en desarrollo y staging
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ForrajeriaJovitaAPI v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health Check Endpoint
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "OK",
    service = "Forrajeria Jovita API",
    version = "1.0.0",
    time = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
})).WithTags("Health");

// Startup Logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 API Forrajeria Jovita Started");
logger.LogInformation("📍 Environment: {Env}", app.Environment.EnvironmentName);

var paywayCfg = app.Services.GetRequiredService<PaywayOptions>();
var paywayEnv = paywayCfg.Environment ?? "sandbox";
var paywayUrl = paywayEnv.ToLower() == "production" ? paywayCfg.LiveApiBaseUrl : paywayCfg.SandboxApiBaseUrl;

logger.LogInformation("💳 Payway Configured - Environment: {Env}, URL: {Url}", paywayEnv, paywayUrl);
logger.LogInformation("🔐 WebhookSecret configured: {Configured}", !string.IsNullOrEmpty(paywayCfg.WebhookSecret));

app.Run();
