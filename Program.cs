using System;
using System.Net.Http.Headers;
using System.Text;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// -------------------- CONFIG: Payway options --------------------
builder.Services.Configure<PaywayOptions>(builder.Configuration.GetSection("Payway"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PaywayOptions>>().Value);

// -------------------- DB --------------------
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// -------------------- HTTP CLIENT (typed) for Payway --------------------
builder.Services.AddHttpClient<IPaywayService, PaywayService>((sp, client) =>
{
    var cfg = sp.GetRequiredService<PaywayOptions>();
    if (string.IsNullOrWhiteSpace(cfg.ApiUrl))
        throw new Exception("Payway ApiUrl not configured in configuration (Payway:ApiUrl).");

    var baseUrl = cfg.ApiUrl.EndsWith("/") ? cfg.ApiUrl : cfg.ApiUrl + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

// -------------------- Authentication (JWT) --------------------
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("Jwt:Key not set");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new Exception("Jwt:Issuer not set");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new Exception("Jwt:Audience not set");

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// -------------------- App services --------------------
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IStockService, StockService>();

// -------------------- MVC / JSON / Swagger --------------------
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = null;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ForrajeriaJovitaAPI", Version = "v1" });
});

// -------------------- FORWARDED HEADERS (important behind proxies like Render) --------------------
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // optionally add known proxies/networks if you want to lock it down
});

// -------------------- CORS --------------------
// Allow specific origins. If usás cookies, necesitás AllowCredentials.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://forrajeria-jovita.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // si no usás credenciales, podés quitar esto
    });
});

var app = builder.Build();

// -------------------- Pipeline --------------------
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Forward headers *before* anything that depends on scheme/origin
app.UseForwardedHeaders();

// Routing before CORS for endpoint routing model
app.UseRouting();

// Logging middleware to inspect incoming Origin and Method (helps diagnosticar)
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers["Origin"].ToString();
    var method = context.Request.Method;
    app.Logger.LogInformation("Incoming request: {Method} {Path} Origin={Origin}", method, context.Request.Path, string.IsNullOrEmpty(origin) ? "(none)" : origin);
    await next();
});

// IMPORTANT: CORS BEFORE auth and endpoints
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// -------------------- Startup logs --------------------
try
{
    var payway = app.Services.GetRequiredService<PaywayOptions>();
    app.Logger.LogInformation("💳 Payway ApiUrl: {Url}", payway.ApiUrl);
    app.Logger.LogInformation(
        "🔑 Keys present: Public={Pub} Private={Priv} Site={Site}",
        !string.IsNullOrEmpty(payway.PublicKey),
        !string.IsNullOrEmpty(payway.PrivateKey),
        !string.IsNullOrEmpty(payway.SiteId)
    );
}
catch (Exception ex)
{
    app.Logger.LogWarning("Payway options not available at startup: {Message}", ex.Message);
}

app.Run();

