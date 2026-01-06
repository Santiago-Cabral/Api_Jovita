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

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// LOGGING CONFIGURATION
// ============================================================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// ============================================================
// CONFIGURATION / SETTINGS BINDING
// ============================================================
// Bind Payway settings to a strongly-typed POCO (see Settings/PaywaySettings.cs)
builder.Services.Configure<PaywaySettings>(builder.Configuration.GetSection("Payway"));

// ============================================================
// DATABASE
// ============================================================
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ============================================================
// HTTP CLIENT (Para Payway y otras integraciones)
// - Se registran HttpClientFactory y un cliente nombrado para Payway si lo deseas
// ============================================================
builder.Services.AddHttpClient(); // IHttpClientFactory disponible para inyectar

// (Opcional) Cliente nombrado para Payway: inyectable con IHttpClientFactory.CreateClient("payway")
builder.Services.AddHttpClient("payway", client =>
{
    var apiUrl = builder.Configuration["Payway:ApiUrl"];
    if (!string.IsNullOrEmpty(apiUrl))
    {
        client.BaseAddress = new Uri(apiUrl.TrimEnd('/'));
    }
    client.Timeout = TimeSpan.FromSeconds(30);
});

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
// Seguridad / Auth / Aplicación
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IClientAccountService, ClientAccountService>();

// Servicios de negocio
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();

// Payway service (integration)
builder.Services.AddScoped<IPaywayService, PaywayService>();

// ============================================================
// CONTROLLERS / JSON
// ============================================================
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.WriteIndented = true;
        o.JsonSerializerOptions.PropertyNamingPolicy = null; // Mantener PascalCase si lo prefieres
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
// SWAGGER (añadir seguridad básica para testing si quieres)
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ForrajeriaJovitaAPI", Version = "v1" });

    // Opcional: agregar esquema de seguridad para JWT (para probar desde Swagger)
    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Description = "Bearer JWT Authorization header",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };
    c.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtSecurityScheme, Array.Empty<string>() }
    });
});

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

// Si quieres ejecutar migraciones automáticas en startup (opcional, manejar con cuidado en prod)
// using (var scope = app.Services.CreateScope())
// {
//     var db = scope.ServiceProvider.GetRequiredService<ForrajeriaContext>();
//     db.Database.Migrate();
// }

app.Run();
