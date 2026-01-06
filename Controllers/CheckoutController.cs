using System;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CheckoutController : ControllerBase
    {
        private readonly ICheckoutService _checkoutService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(ICheckoutService checkoutService, ILogger<CheckoutController> logger)
        {
            _checkoutService = checkoutService;
            _logger = logger;
        }

        /// <summary>
        /// Inicia el checkout: crea venta / descuenta stock / crea checkout en Payway y devuelve la URL para redirigir.
        /// POST: /api/checkout/start
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> Start([FromBody] CheckoutRequestDto request)
        {
            try
            {
                var result = await _checkoutService.ProcessCheckoutAsync(request);

                if (string.IsNullOrWhiteSpace(result.PaywayRedirectUrl))
                {
                    // Esto normalmente no debería pasar: si Payway falla, el service lanza excepción.
                    _logger.LogError("No se generó paywayRedirectUrl para la venta {SaleId}", result.SaleId);
                    return StatusCode(500, new { error = "No se pudo generar la URL de pago." });
                }

                return Ok(result);
            }
            catch (ArgumentException aex)
            {
                return BadRequest(new { error = aex.Message });
            }
            catch (InvalidOperationException ioex)
            {
                // negocio / estado inválido (p. ej. stock / payway)
                _logger.LogWarning(ioex, "Error de negocio al iniciar checkout");
                return StatusCode(500, new { error = ioex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al iniciar checkout");
                return StatusCode(500, new { error = "Error interno al iniciar checkout" });
            }
        }

        /// <summary>
        /// Endpoint opcional para consultar el estado de la venta desde el frontend
        /// GET: api/checkout/{saleId}/status
        /// </summary>
        [HttpGet("{saleId}/status")]
        public async Task<IActionResult> Status(int saleId, [FromServices] ForrajeriaContext context)
        {
            var sale = await context.Sales.FindAsync(saleId);
            if (sale == null) return NotFound(new { error = "Venta no encontrada" });

            // Exponer un estado simple (ajusta según tu modelo: PaymentStatus, etc)
            return Ok(new
            {
                SaleId = sale.Id,
                Status = sale.PaymentStatus, // si tu modelo usa PaymentStatus int
                Total = sale.Total
            });
        }
    }
}

