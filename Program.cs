using System.Net.Http.Headers;
using System.Text;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Security;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// DB
// =====================================================
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"))
);

// =====================================================
// PAYWAY
// =====================================================
builder.Services.Configure<PaywayOptions>(
    builder.Configuration.GetSection("Payway"));

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<PaywayOptions>>().Value);

builder.Services.AddHttpClient<IPaywayService, PaywayService>((sp, client) =>
{
    var cfg = sp.GetRequiredService<PaywayOptions>();

    if (string.IsNullOrWhiteSpace(cfg.ApiUrl))
        throw new Exception("Payway ApiUrl not configured");

    client.BaseAddress = new Uri(
        cfg.ApiUrl.EndsWith("/") ? cfg.ApiUrl : cfg.ApiUrl + "/");

    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

// =====================================================
// JWT
// =====================================================
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new Exception("Jwt:Key missing");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new Exception("Jwt:Issuer missing");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new Exception("Jwt:Audience missing");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// =====================================================
// DEPENDENCY INJECTION (CRÍTICO)
// =====================================================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IStockService, StockService>();

// =====================================================
// CONTROLLERS / JSON
// =====================================================
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = null;
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            JsonIgnoreCondition.WhenWritingNull;
    });

// =====================================================
// SWAGGER
// =====================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ForrajeriaJovitaAPI",
        Version = "v1"
    });
});

// =====================================================
// CORS
// =====================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://forrajeria-jovita.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// =====================================================
// FORWARDED HEADERS (RENDER)
// =====================================================
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
});

// =====================================================
// BUILD
// =====================================================
var app = builder.Build();

// =====================================================
// PIPELINE
// =====================================================
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseForwardedHeaders();

app.UseRouting();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// =====================================================
// STARTUP LOG
// =====================================================
try
{
    var payway = app.Services.GetRequiredService<PaywayOptions>();
    app.Logger.LogInformation("Payway ApiUrl: {Url}", payway.ApiUrl);
}
catch { }

app.Run();


