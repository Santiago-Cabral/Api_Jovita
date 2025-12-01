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
// CORS (NATIVO, CORRECTO, DEFINITIVO)
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFront", p =>
    {
        p.WithOrigins(
            "http://localhost:5173",
            "https://localhost:5173",
            "https://forrajeria-jovita.onrender.com",
            "https://forrajeria-jovita-api.onrender.com"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// ============================================================
// JWT
// ============================================================
var key = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(key))
    throw new Exception("Jwt:Key no configurado.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

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
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
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
// MIDDLEWARE ORDER (.NET 9 EXIGE ESTO)
// ============================================================
app.UseRouting();

app.UseCors("AllowFront"); // 🔥 CORS ACTIVO Y EN EL LUGAR CORRECTO

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

// ============================================================
// CONTROLLERS
// ============================================================
app.MapControllers().RequireCors("AllowFront");

// ============================================================
// HEALTH CHECK (OK)
// ============================================================
app.MapGet("/api/health", () =>
{
    return Results.Json(new
    {
        status = "OK",
        message = "Forrajeria Jovita API online",
        time = DateTime.UtcNow
    });
}).RequireCors("AllowFront");

// ============================================================
// RUN
// ============================================================
app.Run();
