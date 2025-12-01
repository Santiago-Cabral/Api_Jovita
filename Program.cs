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
// ⚠️ IMPORTANTE: SIN AddCors, SIN políticas. Usamos CORS MANUAL.
// ============================================================

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
// 🌐 CORS MANUAL (SIMPLE, GLOBAL, FUNCIONA EN TODO)
// ============================================================
// No usamos credenciales/cookies, así que podemos usar '*'
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
    ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
    ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";

    // Responder preflight OPTIONS acá mismo
    if (ctx.Request.Method == "OPTIONS")
    {
        ctx.Response.StatusCode = 200;
        await ctx.Response.CompleteAsync();
        return;
    }

    await next();
});

// ============================================================
// PIPELINE
// ============================================================
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

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
