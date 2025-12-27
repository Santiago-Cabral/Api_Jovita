using System.Text;
using System.Text.Json.Serialization;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Security;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// LOGGING CONFIGURATION
// ============================================================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// ============================================================
// DATABASE
// ============================================================
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ============================================================
// JWT SETTINGS
// ============================================================
var jwtSettings = new JwtSettings
{
    Key = builder.Configuration["Jwt:Key"] ?? throw new Exception("Jwt:Key no configurado"),
    Issuer = builder.Configuration["Jwt:Issuer"] ?? throw new Exception("Jwt:Issuer no configurado"),
    Audience = builder.Configuration["Jwt:Audience"] ?? throw new Exception("Jwt:Audience no configurado"),
    ExpiresMinutes = string.IsNullOrEmpty(builder.Configuration["Jwt:ExpiresMinutes"])
        ? 60
        : int.Parse(builder.Configuration["Jwt:ExpiresMinutes"]!)
};
builder.Services.AddSingleton(jwtSettings);

// ============================================================
// SERVICES (DEPENDENCY INJECTION)
// ============================================================
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IClientAccountService, ClientAccountService>();

// VentaService registration
// Standard registration is sufficient since VentaService only needs ForrajeriaContext
builder.Services.AddScoped<IVentaService, VentaService>();

// ============================================================
// CONTROLLERS / JSON
// ============================================================
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.WriteIndented = true;
        o.JsonSerializerOptions.PropertyNamingPolicy = null; // Mantener PascalCase
    });

// ============================================================
// CORS - PRODUCCIÓN (solo frontends válidos)
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",                 // Dev local
                "http://localhost:3000",                 // Otro dev
                "https://forrajeria-jovita.vercel.app"   // Frontend en producción
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ============================================================
// AUTHENTICATION (JWT)
// ============================================================
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = signingKey
        };
    });

// ============================================================
// SWAGGER
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ============================================================
// PIPELINE
// ============================================================
app.UseHttpsRedirection();

// 1. Routing
app.UseRouting();

// 2. CORS (DESPUÉS de Routing, ANTES de Auth)
app.UseCors("AllowFrontend");

// 3. Auth
app.UseAuthentication();
app.UseAuthorization();

// 4. Swagger
app.UseSwagger();
app.UseSwaggerUI();

// 5. Controllers
app.MapControllers();

// 6. Health check
app.MapGet("/api/health", () =>
{
    return Results.Json(new
    {
        status = "OK",
        message = "Forrajeria Jovita API online",
        time = DateTime.UtcNow,
        environment = app.Environment.EnvironmentName
    });
});

// Log startup info
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 Forrajeria Jovita API iniciada");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("CORS habilitado para: http://localhost:5173, https://forrajeria-jovita.vercel.app");

app.Run();