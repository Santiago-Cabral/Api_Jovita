using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar puerto de Railway
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Configurar DbContext
builder.Services.AddDbContext<ForrajeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Registrar Servicios
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IBranchService, BranchService>();

// Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Controllers y API
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();

// Configurar Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Forrajeria Jovita API",
        Version = "v1",
        Description = "API para gestión de forrajería",
        Contact = new OpenApiContact
        {
            Name = "Forrajeria Jovita"
        }
    });
    c.OrderActionsBy(apiDesc => apiDesc.RelativePath);
});

var app = builder.Build();

// Swagger siempre disponible
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Forrajeria Jovita API v1");
    c.RoutePrefix = string.Empty;
    c.DocumentTitle = "Forrajeria Jovita API";
    c.DefaultModelsExpandDepth(-1);
});

// CORS
app.UseCors("AllowAll");

app.UseAuthorization();
app.MapControllers();

// Endpoint de prueba
app.MapGet("/api/health", () => new
{
    status = "OK",
    message = "API funcionando correctamente",
    timestamp = DateTime.Now,
    environment = Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT") ?? "local"
})
.WithTags("Health Check");

app.Run();