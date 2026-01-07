using System.Text;
using System.Text.Json.Serialization;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Security;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();

builder.Services.AddHttpClient("payway", client =>
{
    var baseUrl = builder.Configuration["Payway:BaseUrl"] ?? "https://api.decidir.com";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("Jwt:Key no configurado");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new Exception("Jwt:Issuer no configurado");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new Exception("Jwt:Audience no configurado");

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
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = signingKey
        };
    });

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
builder.Services.AddScoped<IPaywayService, PaywayService>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
        o.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000",
                "https://forrajeria-jovita.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ForrajeriaJovitaAPI",
        Version = "v1",
        Description = "API para Forrajería Jovita - Sistema de ventas y pagos"
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT con el prefijo 'Bearer '",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    c.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ForrajeriaJovitaAPI v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "OK",
    service = "Forrajeria Jovita API",
    version = "1.0.0",
    time = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
})).WithTags("Health");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 API Forrajeria Jovita iniciada");
logger.LogInformation("📍 Environment: {Env}", app.Environment.EnvironmentName);
logger.LogInformation("🌐 CORS habilitado para: localhost:5173, localhost:3000, forrajeria-jovita.vercel.app");
logger.LogInformation("💳 Payway configurado en: {PaywayUrl}", builder.Configuration["Payway:BaseUrl"]);

app.Run();