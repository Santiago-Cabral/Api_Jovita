using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Security;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------
// CONFIG: Payway options from appsettings / user secrets
// -----------------------------
builder.Services.Configure<PaywayOptions>(builder.Configuration.GetSection("Payway"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PaywayOptions>>().Value);

// -----------------------------
// DATABASE
// -----------------------------
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// -----------------------------
// HTTP CLIENT for IPaywayService
// -----------------------------
builder.Services.AddHttpClient<IPaywayService, PaywayService>((sp, client) =>
{
    var cfg = sp.GetRequiredService<PaywayOptions>();
    if (string.IsNullOrEmpty(cfg.ApiUrl))
        throw new Exception("Payway:ApiUrl not configured");

    // Use the ApiUrl as BaseAddress (caller MUST ensure it's correct)
    client.BaseAddress = new Uri(cfg.ApiUrl);
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ForrajeriaJovitaAPI/1.0");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
}).SetHandlerLifetime(TimeSpan.FromMinutes(5));

// -----------------------------
// AUTH (JWT)
// -----------------------------
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("Jwt:Key missing");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new Exception("Jwt:Issuer missing");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new Exception("Jwt:Audience missing");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

// -----------------------------
// APP SERVICES
// -----------------------------
builder.Services.AddScoped<IPaywayService, PaywayService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
// add other services as needed...

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
        o.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ForrajeriaJovitaAPI", Version = "v1" });
});

// -----------------------------
// CORS: allow only specific origins (no wildcard when credentials are allowed)
// -----------------------------
var allowedOrigins = new[]
{
    "http://localhost:5173",
    "http://localhost:3000",
    "https://forrajeria-jovita.vercel.app"
};

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // si no usás credenciales (cookies/auth headers) podés quitar AllowCredentials()
    });
});

// -----------------------------
// BUILD
// -----------------------------
var app = builder.Build();

// -----------------------------
// GLOBAL EXCEPTION HANDLER que añade headers CORS cuando hay errores
// -----------------------------
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var exFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var err = exFeature?.Error;

        // Reproducible: respetar origen si está en allowedOrigins para evitar wildcard con AllowCredentials
        var origin = context.Request.Headers["Origin"].FirstOrDefault();
        if (!string.IsNullOrEmpty(origin) && allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Vary"] = "Origin";
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 500;

        var payload = new
        {
            error = "Internal Server Error",
            message = err?.Message,
            detail = err?.InnerException?.Message
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        await context.Response.WriteAsync(json);
    });
});

// -----------------------------
// MIDDLEWARE ORDER: routing -> cors -> auth -> endpoints
// -----------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1"); });
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1"); });
}

app.UseHttpsRedirection();
app.UseRouting();

// IMPORTANT: CORS must be applied before Authentication/Authorization and before endpoints
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// -----------------------------
// LOG STARTUP & PAYWAY INFO
// -----------------------------
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 ForrajeriaJovitaAPI started. Env: {Env}", app.Environment.EnvironmentName);

var paywayOpts = app.Services.GetRequiredService<PaywayOptions>();
logger.LogInformation("💳 Payway ApiUrl: {Url}", paywayOpts.ApiUrl);
logger.LogInformation("🔑 Keys present: Public={Pub} Private={Priv} Site={Site}",
    !string.IsNullOrEmpty(paywayOpts.PublicKey),
    !string.IsNullOrEmpty(paywayOpts.PrivateKey),
    !string.IsNullOrEmpty(paywayOpts.SiteId));

app.Run();
