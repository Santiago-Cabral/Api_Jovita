using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Dtos;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly ForrajeriaContext _context;
        private readonly ILogger<SettingsController> _logger;
        private readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        };

        public SettingsController(ForrajeriaContext context, ILogger<SettingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/Settings - Obtener configuración pública tipada
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<SettingsDto>> GetSettings()
        {
            try
            {
                var raw = await _context.Settings
                    .AsNoTracking()
                    .ToDictionaryAsync(s => s.Key, s => s.Value);

                if (!raw.Any())
                {
                    _logger.LogInformation("No hay settings en BD, devolviendo defaults");
                    return Ok(SettingsDto.Defaults());
                }

                var dto = SettingsMapper.FromDictionary(raw);
                return Ok(dto);
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
        public async Task<IActionResult> UpdateSettings([FromBody] SettingsDto updates)
        {
            if (updates == null)
            {
                return BadRequest(new { message = "Payload inválido" });
            }

            try
            {
                // Verificar rol del usuario
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value?.ToLower();
                if (userRole != "admin" && userRole != "administrador" && userRole != "administrador/a")
                {
                    _logger.LogWarning("Usuario sin permisos intentó actualizar settings. Role: {Role}", userRole);
                    return Forbid();
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
                _logger.LogInformation("Usuario {UserId} actualizando settings", userId);

                // Convertir DTO a diccionario (key -> JSON string)
                var dict = ToDictionary(updates);

                foreach (var kvp in dict)
                {
                    var existing = await _context.Settings.FirstOrDefaultAsync(s => s.Key == kvp.Key);

                    if (existing == null)
                    {
                        existing = new Setting
                        {
                            Key = kvp.Key,
                            Value = kvp.Value,
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = userId
                        };
                        _context.Settings.Add(existing);
                        _logger.LogInformation("Creando setting: {Key}", kvp.Key);
                    }
                    else
                    {
                        existing.Value = kvp.Value;
                        existing.UpdatedAt = DateTime.UtcNow;
                        existing.UpdatedBy = userId;
                        _logger.LogInformation("Actualizando setting: {Key}", kvp.Key);
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Settings guardados exitosamente por {UserId}", userId);
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
                if (userRole != "admin" && userRole != "administrador" && userRole != "administrador/a")
                {
                    return Forbid();
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
                _logger.LogWarning("Usuario {UserId} reseteando settings a defaults", userId);

                var allSettings = await _context.Settings.ToListAsync();
                if (allSettings.Any())
                {
                    _context.Settings.RemoveRange(allSettings);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "Settings reseteados a valores por defecto" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al resetear settings");
                return StatusCode(500, new { message = "Error al resetear configuración" });
            }
        }

        #region Helpers

        /// <summary>
        /// Convierte SettingsDto a diccionario key->jsonString para almacenar en la tabla Settings.
        /// Las claves usadas son las mismas que las propiedades JSON (camelCase).
        /// </summary>
        private Dictionary<string, string> ToDictionary(SettingsDto dto)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Strings
            d["storeName"] = JsonSerializer.Serialize(dto.StoreName, _jsonOpts);
            d["email"] = JsonSerializer.Serialize(dto.Email, _jsonOpts);
            d["phone"] = JsonSerializer.Serialize(dto.Phone, _jsonOpts);
            d["address"] = JsonSerializer.Serialize(dto.Address, _jsonOpts);
            d["description"] = JsonSerializer.Serialize(dto.Description, _jsonOpts);
            d["storeLocation"] = JsonSerializer.Serialize(dto.StoreLocation, _jsonOpts);

            // Booleans / numbers
            d["freeShipping"] = JsonSerializer.Serialize(dto.FreeShipping, _jsonOpts);
            d["freeShippingMinimum"] = JsonSerializer.Serialize(dto.FreeShippingMinimum, _jsonOpts);
            d["shippingCost"] = JsonSerializer.Serialize(dto.ShippingCost, _jsonOpts);
            d["deliveryTime"] = JsonSerializer.Serialize(dto.DeliveryTime, _jsonOpts);

            // Shipping zones (array)
            d["shippingZones"] = JsonSerializer.Serialize(dto.ShippingZones ?? new List<ShippingZoneDto>(), _jsonOpts);

            d["defaultShippingPrice"] = JsonSerializer.Serialize(dto.DefaultShippingPrice, _jsonOpts);
            d["cash"] = JsonSerializer.Serialize(dto.Cash, _jsonOpts);
            d["bankTransfer"] = JsonSerializer.Serialize(dto.BankTransfer, _jsonOpts);
            d["cards"] = JsonSerializer.Serialize(dto.Cards, _jsonOpts);

            d["bankName"] = JsonSerializer.Serialize(dto.BankName, _jsonOpts);
            d["accountHolder"] = JsonSerializer.Serialize(dto.AccountHolder, _jsonOpts);
            d["cbu"] = JsonSerializer.Serialize(dto.Cbu, _jsonOpts);
            d["alias"] = JsonSerializer.Serialize(dto.Alias, _jsonOpts);

            d["emailNewOrder"] = JsonSerializer.Serialize(dto.EmailNewOrder, _jsonOpts);
            d["emailLowStock"] = JsonSerializer.Serialize(dto.EmailLowStock, _jsonOpts);
            d["whatsappNewOrder"] = JsonSerializer.Serialize(dto.WhatsappNewOrder, _jsonOpts);
            d["whatsappLowStock"] = JsonSerializer.Serialize(dto.WhatsappLowStock, _jsonOpts);

            return d;
        }

        #endregion
    }
}
