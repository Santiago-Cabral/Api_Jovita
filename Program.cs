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
// CONTROLLERS + JSON OPTIONS
// ============================================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// ============================================================
// CORS PARA .NET 9 + RENDER + JWT
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFront", policy =>
    {
        policy
            .SetIsOriginAllowed(origin => true)  // PERMITE localhost, IP local, Render, etc
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ============================================================
// JWT AUTH
// ============================================================
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new Exception("Falta Jwt:Key en appsettings.json o variables de entorno.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

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

// ============================================================
// BUILD APP
// ============================================================
var app = builder.Build();

// ============================================================
// SWAGGER UI
// ============================================================
app.UseSwagger();
app.UseSwaggerUI();

// ============================================================
// ORDER MIDDLEWARE
// ============================================================
app.UseRouting();

app.UseCors("AllowFront");   // OBLIGATORIO aquí

app.UseAuthentication();
app.UseAuthorization();

// =============================
// CONTROLLERS
// =============================
app.MapControllers().RequireCors("AllowFront");

// =============================
// HEALTH ENDPOINT (NET 9 REQUIERE CORS EXPLÍCITO)
// =============================
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "OK",
    message = "Forrajeria Jovita API online",
    time = DateTime.UtcNow
}))
.RequireCors("AllowFront");   // ¡ESTO ES LA CLAVE!

// ============================================================
// RUN
// ============================================================
app.Run();

