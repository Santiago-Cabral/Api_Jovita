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

var builder = WebApplication.CreateBuilder(args);

// -------------------- CONFIG: Payway options --------------------
builder.Services.Configure<PaywayOptions>(builder.Configuration.GetSection("Payway"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PaywayOptions>>().Value);

// -------------------- DB --------------------
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// -------------------- HTTP CLIENT (typed) for Payway --------------------
// Use a single registration via AddHttpClient<TService, TImpl>() so the HttpClient BaseAddress is set.
builder.Services.AddHttpClient<IPaywayService, PaywayService>((sp, client) =>
{
    var cfg = sp.GetRequiredService<PaywayOptions>();
    if (string.IsNullOrWhiteSpace(cfg.ApiUrl))
        throw new Exception("Payway ApiUrl not configured in configuration (Payway:ApiUrl).");

    // Ensure trailing slash to avoid invalid URI issues
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

// -------------------- App services (keep existing ones) --------------------
// Do NOT register IPaywayService again (AddHttpClient already did it).
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IStockService, StockService>(); // si tenés este servicio; ajustá si el nombre es otro

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

// -------------------- CORS --------------------
// Permití tus frontends; en producción preferí restringir.
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

var app = builder.Build();

// -------------------- Pipeline --------------------
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseRouting();

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
