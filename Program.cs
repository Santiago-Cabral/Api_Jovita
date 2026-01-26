using System.Text;
using System.Net.Http.Headers;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Security;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ========================
// PAYWAY CONFIG
// ========================
builder.Services.Configure<PaywayOptions>(
    builder.Configuration.GetSection("Payway")
);
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<PaywayOptions>>().Value
);

// ========================
// DB
// ========================
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ========================
// HTTP CLIENT PAYWAY
// ========================
builder.Services.AddHttpClient<IPaywayService, PaywayService>((sp, client) =>
{
    var cfg = sp.GetRequiredService<PaywayOptions>();

    if (string.IsNullOrEmpty(cfg.ApiUrl))
        throw new Exception("Payway ApiUrl not configured");

    client.BaseAddress = new Uri(cfg.ApiUrl);
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json")
    );
});

// ========================
// JWT
// ========================
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

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
            Encoding.UTF8.GetBytes(jwtKey)
        )
    };
});

// ========================
// SERVICES (🔥 ACÁ ESTABA EL ERROR 🔥)
// ========================
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IPaywayService, PaywayService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ========================
// CORS
// ========================
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("AllowFrontend", p =>
    {
        p.WithOrigins(
            "http://localhost:5173",
            "https://forrajeria-jovita.vercel.app"
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ========================
// LOG STARTUP
// ========================
var payway = app.Services.GetRequiredService<PaywayOptions>();
app.Logger.LogInformation("💳 Payway ApiUrl: {Url}", payway.ApiUrl);
app.Logger.LogInformation(
    "🔑 Keys present: Public={Pub} Private={Priv} Site={Site}",
    !string.IsNullOrEmpty(payway.PublicKey),
    !string.IsNullOrEmpty(payway.PrivateKey),
    !string.IsNullOrEmpty(payway.SiteId)
);

app.Run();
