using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Security;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configurar puerto Railway
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// =============================
// CONFIGURAR DB CONTEXT
// =============================
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =============================
// REGISTRAR SERVICIOS EXISTENTES
// =============================
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IBranchService, BranchService>();


// =============================
// JWT CONFIG
// =============================
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("Jwt").Bind(jwtSettings);
builder.Services.AddSingleton(jwtSettings);

// Servicios AUTH
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IClientAccountService, ClientAccountService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();



// =============================
// JWT AUTHENTICATION
// =============================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
    };
});

builder.Services.AddAuthorization();

// =============================
// CORS
// =============================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// =============================
// CONTROLLERS + JSON
// =============================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();

// =============================
// SWAGGER + JWT
// =============================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Forrajeria Jovita API",
        Version = "v1",
        Description = "API para gestión de forrajería",
        Contact = new OpenApiContact { Name = "Forrajeria Jovita" }
    });

    // Habilitar JWT en Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Ingrese el token JWT: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[]{}
        }
    });

    c.OrderActionsBy(api => api.RelativePath);
});

var app = builder.Build();

// =============================
// SWAGGER SIEMPRE ACTIVO
// =============================
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Forrajeria Jovita API v1");
    c.RoutePrefix = string.Empty;
    c.DocumentTitle = "Forrajeria Jovita API";
    c.DefaultModelsExpandDepth(-1);
});

// =============================
// MIDDLEWARES
// =============================
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// =============================
// HEALTH CHECK
// =============================
app.MapGet("/api/health", () => new
{
    status = "OK",
    message = "API funcionando correctamente",
    timestamp = DateTime.Now,
    environment = Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT") ?? "local"
})
.WithTags("Health Check");

app.Run();
