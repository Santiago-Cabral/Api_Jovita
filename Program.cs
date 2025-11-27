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
// DEPENDENCY INJECTION (SERVICES)
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
// CONTROLLERS + JSON
// ============================================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// ============================================================
// CORS (REACT FRONTEND / MOBILE APPS)
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFront",
        policy =>
        {
            policy
                .AllowAnyOrigin()   // Podés restringirlo si querés
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// ============================================================
// JWT AUTHENTICATION
// ============================================================
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new Exception("Falta Jwt:Key en appsettings.json o variables de entorno.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;   // Para Render
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
// SWAGGER + JWT SUPPORT
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Forrajeria Jovita API",
        Version = "v1",
        Description = "API de productos, ventas, clientes y sucursales."
    });

    // Esquema de autorización JWT Bearer
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Token JWT en formato: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, new string[] {} }
    });
});

// ============================================================
// BUILD APP
// ============================================================
var app = builder.Build();

// ============================================================
// SWAGGER SIEMPRE ACTIVO (Render necesita esto)
// ============================================================
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Forrajeria Jovita API v1");
    c.RoutePrefix = "swagger";  // URL final: /swagger
});

// ============================================================
// HTTPS REDIRECTION SOLO EN DESARROLLO
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ============================================================
// MIDDLEWARE: CORS, AUTH, ROUTING
// ============================================================
app.UseCors("AllowFront");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ============================================================
// HEALTH CHECK
// ============================================================
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "OK",
    message = "Forrajeria Jovita API online",
    time = DateTime.UtcNow
}));

// ============================================================
// RUN
// ============================================================
app.Run();
