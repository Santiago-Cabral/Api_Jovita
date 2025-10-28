using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar URLs
builder.WebHost.UseUrls("http://localhost:5000", "https://localhost:5001");

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
    // Ordenar endpoints por ruta
    c.OrderActionsBy(apiDesc => apiDesc.RelativePath);
});

var app = builder.Build();

// Middleware - Swagger disponible siempre
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Forrajeria Jovita API v1");
    c.RoutePrefix = string.Empty; // Swagger en la raíz: http://localhost:5000
    c.DocumentTitle = "Forrajeria Jovita API";
    c.DefaultModelsExpandDepth(-1); // Ocultar schemas por defecto
});

// CORS
app.UseCors("AllowAll");

// HTTPS Redirection (comentado para desarrollo solo HTTP)
// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Endpoint de prueba
app.MapGet("/api/health", () => new
{
    status = "OK",
    message = "API funcionando correctamente",
    timestamp = DateTime.Now
})
.WithTags("Health Check");

app.Run();