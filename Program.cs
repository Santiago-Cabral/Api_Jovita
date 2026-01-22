// Program.cs
using System;
using System.Net.Security;
using System.Text;
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
using Polly;
using Polly.Extensions.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Bind PaywayOptions (reads from appsettings.json OR User Secrets)
builder.Services.Configure<PaywayOptions>(builder.Configuration.GetSection("Payway"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PaywayOptions>>().Value);

// Logging + DB
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Polly retry policy
static IAsyncPolicy<System.Net.Http.HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

// Add HttpClient for Payway
builder.Services.AddHttpClient<IPaywayService, PaywayService>((sp, client) =>
{
    var cfg = sp.GetRequiredService<PaywayOptions>();

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

    // Decide validation by environment variable or builder.Environment
    var isProduction = (builder.Configuration["Payway:Environment"] ?? builder.Environment.EnvironmentName)?.ToLower() == "production"
                       || builder.Environment.IsProduction();

    if (isProduction)
    {
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            errors == SslPolicyErrors.None;
    }
    else
    {
        handler.ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    return handler;
});

// JWT Auth
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

// DI of other services (leave as you had)
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://localhost:3000", "https://forrajeria-jovita.vercel.app")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Middleware + Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1"); c.RoutePrefix = "swagger"; });
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1"); c.RoutePrefix = "swagger"; });
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "OK",
    service = "Forrajeria Jovita API",
    version = "1.0.0",
    time = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
})).WithTags("Health");

// Startup logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 ForrajeriaJovitaAPI started. Env: {Env}", app.Environment.EnvironmentName);

var paywayOptions = app.Services.GetRequiredService<PaywayOptions>();
logger.LogInformation("💳 Payway env:{Env} sitePresent:{HasSite}", paywayOptions.Environment, !string.IsNullOrEmpty(paywayOptions.SiteId));
logger.LogInformation("🔎 Keys present: Public={Public}, Private={Private}", !string.IsNullOrEmpty(paywayOptions.PublicApiKey), !string.IsNullOrEmpty(paywayOptions.PrivateApiKey));

app.Run();

