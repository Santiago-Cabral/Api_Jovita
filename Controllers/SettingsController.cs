using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly ForrajeriaContext _context;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(ForrajeriaContext context, ILogger<SettingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/Settings - Obtener configuración (público)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<Dictionary<string, object>>> GetSettings()
        {
            try
            {
                var settings = await _context.Settings
                    .ToDictionaryAsync(s => s.Key, s => s.Value);

                if (!settings.Any())
                {
                    _logger.LogInformation("No hay settings en BD, devolviendo defaults");
                    return Ok(GetDefaultSettings());
                }

                var result = new Dictionary<string, object>();
                foreach (var kvp in settings)
                {
                    try
                    {
                        var jsonElement = JsonSerializer.Deserialize<JsonElement>(kvp.Value);
                        result[kvp.Key] = jsonElement;
                    }
                    catch
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener settings");
                return StatusCode(500, new { message = "Error al obtener configuración" });
            }
        }

        /// <summary>
        /// PUT /api/Settings - Actualizar configuración (solo admin)
        /// </summary>
        [HttpPut]
        [Authorize]
        public async Task<IActionResult> UpdateSettings([FromBody] Dictionary<string, object> updates)
        {
            try
            {
                // Verificar que sea admin
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value?.ToLower();
                if (userRole != "admin" &&
                    userRole != "administrador" &&
                    userRole != "administrador/a")
                {
                    _logger.LogWarning("Usuario sin permisos intentó actualizar settings. Role: {Role}", userRole);
                    return Forbid();
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
                _logger.LogInformation("Usuario {UserId} actualizando {Count} settings", userId, updates.Count);

                foreach (var kvp in updates)
                {
                    var setting = await _context.Settings
                        .FirstOrDefaultAsync(s => s.Key == kvp.Key);

                    var jsonValue = JsonSerializer.Serialize(kvp.Value);

                    if (setting == null)
                    {
                        setting = new Setting
                        {
                            Key = kvp.Key,
                            Value = jsonValue,
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = userId
                        };
                        _context.Settings.Add(setting);
                        _logger.LogInformation("Creando setting: {Key}", kvp.Key);
                    }
                    else
                    {
                        setting.Value = jsonValue;
                        setting.UpdatedAt = DateTime.UtcNow;
                        setting.UpdatedBy = userId;
                        _logger.LogInformation("Actualizando setting: {Key}", kvp.Key);
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Settings guardados exitosamente");

                return Ok(new { message = "Settings guardados exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar settings");
                return StatusCode(500, new { message = "Error al guardar configuración", error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/Settings/reset - Resetear a valores por defecto (solo admin)
        /// </summary>
        [HttpPost("reset")]
        [Authorize]
        public async Task<IActionResult> ResetToDefaults()
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value?.ToLower();
                if (userRole != "admin" &&
                    userRole != "administrador" &&
                    userRole != "administrador/a")
                {
                    return Forbid();
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
                _logger.LogWarning("Usuario {UserId} reseteando settings a defaults", userId);

                var allSettings = await _context.Settings.ToListAsync();
                _context.Settings.RemoveRange(allSettings);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Settings reseteados a valores por defecto" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al resetear settings");
                return StatusCode(500, new { message = "Error al resetear configuración" });
            }
        }

        /// <summary>
        /// Valores por defecto
        /// </summary>
        private Dictionary<string, object> GetDefaultSettings()
        {
            return new Dictionary<string, object>
            {
                { "storeName", "Forrajeria Jovita" },
                { "email", "contacto@forrajeriajovita.com" },
                { "phone", "+54 9 3814669135" },
                { "address", "Aragón 32 Yerba Buena, Argentina" },
                { "description", "Tu dietética de confianza con productos naturales y saludables" },
                { "storeLocation", "Yerba Buena, Tucumán" },
                { "freeShipping", true },
                { "freeShippingMinimum", 5000 },
                { "shippingCost", 1500 },
                { "deliveryTime", "24-48 horas" },
                { "shippingZones", new[]
                    {
                        new { id = 1, price = 800, label = "Zona 1 - $800", localities = new[] { "yerba buena", "san pablo", "el portal" } },
                        new { id = 2, price = 1200, label = "Zona 2 - $1200", localities = new[] { "san miguel de tucumán", "san miguel", "centro", "tucumán", "villa carmela", "barrio norte" } },
                        new { id = 3, price = 1800, label = "Zona 3 - $1800", localities = new[] { "tafí viejo", "tafi viejo", "banda del río salí", "alderetes", "las talitas" } }
                    }
                },
                { "defaultShippingPrice", 2500 },
                { "cash", true },
                { "bankTransfer", true },
                { "cards", true },
                { "bankName", "Banco Macro" },
                { "accountHolder", "Forrajeria Jovita S.R.L." },
                { "cbu", "0000003100010000000001" },
                { "alias", "JOVITA.DIETETICA" },
                { "emailNewOrder", true },
                { "emailLowStock", true },
                { "whatsappNewOrder", false },
                { "whatsappLowStock", false }
            };
        }
    }
}