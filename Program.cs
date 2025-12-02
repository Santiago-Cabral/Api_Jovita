using System.Text;
using System.Text.Json.Serialization;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// DATABASE
// ============================================================
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ============================================================
// SERVICES (DEPENDENCY INJECTION)
// ============================================================
// 🔐 Servicios de seguridad (FALTABAN ESTOS)
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// Servicios de negocio
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IClientAccountService, ClientAccountService>();

// ============================================================
// JSON OPTIONS
// ============================================================
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.WriteIndented = true;
    });

// ============================================================
// 🔥 CORS - CONFIGURACIÓN CORRECTA
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",              // React dev local
                "http://localhost:3000",              // React dev alternativo
                "https://forrajeria-jovita.vercel.app", // ⚠️ REEMPLAZA con tu URL real de producción
                "https://tu-dominio.com"              // Si tienes dominio personalizado
            )
            .AllowAnyMethod()                         // GET, POST, PUT, DELETE, OPTIONS
            .AllowAnyHeader();                        // Content-Type, Authorization, etc.

        // 💡 NO agregues .AllowCredentials() si no usas cookies
    });
});

// ============================================================
// JWT AUTHENTICATION
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
// PIPELINE - ORDEN CRÍTICO ⚠️
// ============================================================

// 🔥 1. CORS VA PRIMERO (antes de Authentication/Authorization)
app.UseCors("AllowFrontend");

// 2. Routing
app.UseRouting();

// 3. Authentication y Authorization (después de CORS)
app.UseAuthentication();
app.UseAuthorization();

// 4. Swagger (opcional)
app.UseSwagger();
app.UseSwaggerUI();

// ============================================================
// CONTROLLERS
// ============================================================
app.MapControllers();

// ============================================================
// HEALTH CHECK
// ============================================================
app.MapGet("/api/health", () =>
{
    return Results.Json(new
    {
        status = "OK",
        message = "Forrajeria Jovita API online",
        time = DateTime.UtcNow
    });
});

// ============================================================
// RUN
// ============================================================
app.Run();