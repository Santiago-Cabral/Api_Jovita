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
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// 0. BIND PAYWAY OPTIONS (reads from appsettings.json OR User Secrets)
// -----------------------------------------------------------------------------
builder.Services.Configure<PaywayOptions>(builder.Configuration.GetSection("Payway"));
// Expose PaywayOptions as a singleton for easy access (e.g., in startup logging)
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PaywayOptions>>().Value);

// -----------------------------------------------------------------------------
// 1. LOGGING & DATABASE
// -----------------------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// -----------------------------------------------------------------------------
// 2. POLICIES (Polly) - Retry Policy
// -----------------------------------------------------------------------------
static IAsyncPolicy<System.Net.Http.HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

// -----------------------------------------------------------------------------
// 3. PAYWAY HTTP CLIENT CONFIGURATION (HttpClientFactory + Polly + message handler)
// -----------------------------------------------------------------------------
builder.Services.AddHttpClient<IPaywayService, PaywayService>((sp, client) =>
{
    // Prefer the bound PaywayOptions instance
    var cfg = sp.GetRequiredService<IOptions<PaywayOptions>>().Value;

    var env = (cfg.Environment ?? builder.Configuration["Payway:Environment"] ?? "sandbox").ToLowerInvariant();

    var baseUrl = env == "production"
        ? (cfg.LiveApiBaseUrl ?? builder.Configuration["Payway:LiveApiBaseUrl"] ?? "https://live.decidir.com")
        : (cfg.SandboxApiBaseUrl ?? builder.Configuration["Payway:SandboxApiBaseUrl"] ?? "https://developers.decidir.com");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ForrajeriaJovitaAPI/1.0");
    client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.AddPolicyHandler(GetRetryPolicy())
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new System.Net.Http.HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3
    };

    // Use builder.Configuration / builder.Environment (outer scope) to decide certificate validation
    var envConfig = builder.Configuration["Payway:Environment"]?.ToLower();
    var isProduction = (envConfig == "production") || builder.Environment.IsProduction();

    if (isProduction)
    {
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            errors == SslPolicyErrors.None;
    }
    else
    {
        // WARNING: Accept any cert in dev/sandbox only (useful for internal testing)
        handler.ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    return handler;
});

// -----------------------------------------------------------------------------
// 4. AUTHENTICATION & JWT
// -----------------------------------------------------------------------------
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("Jwt:Key is not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new Exception("Jwt:Issuer is not configured");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new Exception("Jwt:Audience is not configured");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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

// -----------------------------------------------------------------------------
// 5. DEPENDENCY INJECTION (SERVICES)
// -----------------------------------------------------------------------------
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

// NOTE: IPaywayService registered above with AddHttpClient

// -----------------------------------------------------------------------------
// 6. CONTROLLERS & SWAGGER
// -----------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
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
        Description = "Enter JWT token with 'Bearer ' prefix",
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

// -----------------------------------------------------------------------------
// 7. CORS CONFIGURATION
// -----------------------------------------------------------------------------
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

// -----------------------------------------------------------------------------
// 8. MIDDLEWARE PIPELINE
// -----------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ForrajeriaJovitaAPI v1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    // You can keep Swagger enabled for staging if you want, but protect it in prod.
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

// -----------------------------------------------------------------------------
// STARTUP LOGGING & Sanity checks
// -----------------------------------------------------------------------------
var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("🚀 API Forrajeria Jovita Started");
logger.LogInformation("📍 Environment: {Env}", app.Environment.EnvironmentName);

// Read the bound PaywayOptions singleton
var paywayCfg = app.Services.GetRequiredService<PaywayOptions>();

var paywayEnv = paywayCfg.Environment ?? builder.Configuration["Payway:Environment"] ?? "sandbox";
var isProd = paywayEnv.Equals("production", StringComparison.OrdinalIgnoreCase);

var paywayUrl = isProd
    ? (paywayCfg.LiveApiBaseUrl ?? builder.Configuration["Payway:LiveApiBaseUrl"])
    : (paywayCfg.SandboxApiBaseUrl ?? builder.Configuration["Payway:SandboxApiBaseUrl"]);

logger.LogInformation("💳 Payway Configured - Environment: {Env}, URL: {Url}", paywayEnv, paywayUrl ?? "default");
logger.LogInformation("🔐 WebhookSecret configured: {Configured}", !string.IsNullOrEmpty(paywayCfg.WebhookSecret));

// Optional: Log whether required keys exist (don't log actual secrets)
logger.LogInformation("🔎 Payway Keys present: PublicKey={PublicPresent}, PrivateKey={PrivatePresent}",
    !string.IsNullOrEmpty(paywayCfg.PublicApiKey), !string.IsNullOrEmpty(paywayCfg.PrivateApiKey));

app.Run();
