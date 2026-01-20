using System.Text;
using System.Text.Json.Serialization;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Security;
using ForrajeriaJovitaAPI.Services;
// Ensure this namespace is imported for the Interface
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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
// 2. PAYWAY HTTP CLIENT CONFIGURATION
// ==============================================================================

// This registers IPaywayService and PaywayService via HttpClient Factory.
// It effectively does "AddScoped<IPaywayService, PaywayService>" but with HttpClient injection.
builder.Services.AddHttpClient<IPaywayService, PaywayService>(client =>
{
    // Settings for the HTTP Client
    client.Timeout = TimeSpan.FromSeconds(45); // Extended timeout for payments
    client.DefaultRequestHeaders.Add("User-Agent", "ForrajeriaJovitaAPI/1.0");
    client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3
    };

    // SSL Certificate Validation Logic (Critical for Sandbox/Dev)
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
    {
        var env = builder.Configuration["Payway:Environment"]?.ToLower();
        var isProduction = env == "production";

        if (isProduction)
        {
            // In Production: Strict validation
            return errors == System.Net.Security.SslPolicyErrors.None;
        }

        // In Sandbox/Dev: Allow potential self-signed certs (common in some payment gateways' test envs)
        return true;
    };

    return handler;
});

// ==============================================================================
// 3. AUTHENTICATION & JWT
// ==============================================================================
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("Jwt:Key is not configured in appsettings.json");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new Exception("Jwt:Issuer is not configured");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new Exception("Jwt:Audience is not configured");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // Set to true in Production
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
// 4. DEPENDENCY INJECTION (SERVICES)
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

// NOTE: IPaywayService is already registered in step 2 via AddHttpClient. 
// Do not add it again here.

// ==============================================================================
// 5. CONTROLLERS & SWAGGER
// ==============================================================================
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.PropertyNamingPolicy = null; // Keeps PascalCase as defined in C# models
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
// 6. CORS CONFIGURATION
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
// 7. MIDDLEWARE PIPELINE
// ==============================================================================

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// Swagger is enabled in all environments for ease of testing, 
// strictly you might want it only in Development.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ForrajeriaJovitaAPI v1");
    c.RoutePrefix = "swagger";
});

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

var paywayEnv = builder.Configuration["Payway:Environment"] ?? "sandbox";
var paywayUrl = paywayEnv.ToLower() == "production"
    ? "https://live.decidir.com/api/v2"
    : "https://developers.decidir.com/api/v2";

logger.LogInformation("💳 Payway Configured - Environment: {Env}, URL: {Url}", paywayEnv, paywayUrl);

app.Run();