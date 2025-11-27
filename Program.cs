using System.Text;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. CONFIGURACIÓN DE BASE DE DATOS
// ============================================================
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ============================================================
// 2. INYECCIÓN DE DEPENDENCIAS (SERVICIOS)
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
// 3. CONFIGURACIÓN DE CONTROLADORES Y JSON
// ============================================================
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Evitar problemas de referencias cíclicas con EF (Sales -> Items -> Product -> Sales...)
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// ============================================================
// 4. CORS (PARA FRONTEND REACT / OTROS CLIENTES)
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFront",
        policy =>
        {
            policy
                .AllowAnyOrigin()       // O mejor: .WithOrigins("https://tu-front.vercel.app")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// ============================================================
// 5. CONFIGURACIÓN JWT
//    Asegúrate de tener en appsettings.json:
//    "Jwt": { "Key": "...supersecreta...", "Issuer": "ForrajeriaJovitaAPI", "Audience": "ForrajeriaJovitaAPI" }
// ============================================================
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new Exception("La clave JWT (Jwt:Key) no está configurada en appsettings.");
}

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = false; // ponlo en true en producción con HTTPS

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = key
        };
    });

// ============================================================
// 6. SWAGGER + CONFIGURACIÓN DE JWT EN SWAGGER
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Forrajeria Jovita API",
        Version = "v1",
        Description = "API para gestión de productos, ventas, clientes y sucursales de Forrajería Jovita."
    });

    // Configurar el esquema de seguridad para JWT
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Introduce el token JWT con el esquema **Bearer**. Ejemplo: `Bearer eyJhbGciOiJI...`",
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
        { securityScheme, Array.Empty<string>() }
    });
});

// ============================================================
// 7. CONSTRUIR APP
// ============================================================
var app = builder.Build();

// ============================================================
// 8. MIDDLEWARE
// ============================================================

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Forrajeria Jovita API v1");
        c.RoutePrefix = string.Empty; // Swagger en la raíz: https://tudominio/
    });
}
else
{
    // Si quieres Swagger también en prod, quita este if y deja siempre:
    // app.UseSwagger();
    // app.UseSwaggerUI();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Forrajeria Jovita API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// CORS
app.UseCors("AllowFront");

// Auth
app.UseAuthentication();
app.UseAuthorization();

// ============================================================
// 9. ENDPOINT SIMPLE DE SALUD (HEALTH CHECK BÁSICO)
// ============================================================
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "OK",
    message = "Forrajeria Jovita API funcionando correctamente",
    time = DateTime.UtcNow
}));

// ============================================================
// 10. MAPEO DE CONTROLADORES
// ============================================================
app.MapControllers();

// ============================================================
// 11. RUN
// ============================================================
app.Run();
