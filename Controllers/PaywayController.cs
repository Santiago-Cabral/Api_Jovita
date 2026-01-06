// Controllers/PaywayController.cs
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.DTOs.Payway;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;

namespace ForrajeriaJovitaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaywayController : ControllerBase
    {
        private readonly IPaywayService _paywayService;
        private readonly ILogger<PaywayController> _logger;
        private readonly ForrajeriaContext _context;

        public PaywayController(
            IPaywayService paywayService,
            ILogger<PaywayController> logger,
            ForrajeriaContext context)
        {
            _paywayService = paywayService;
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// Crear checkout en Payway Ventas Online (Forms)
        /// POST: api/Payway/create-checkout
        /// </summary>
        [HttpPost("create-checkout")]
        public async Task<IActionResult> CreateCheckout(
            [FromBody] PaywayCheckoutRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("📤 Iniciando checkout Payway para Sale ID: {SaleId}", request.SaleId);

                // Validaciones básicas
                if (request.SaleId <= 0 || request.Amount <= 0)
                {
                    return BadRequest(new { error = "SaleId y Amount son requeridos y deben ser mayores a 0" });
                }

                if (string.IsNullOrEmpty(request.Customer?.Email))
                {
                    return BadRequest(new { error = "Email del cliente es requerido" });
                }

                // Crear checkout en Payway
                var result = await _paywayService.CreatePaymentAsync(request);

                // Guardar transacción en base de datos
                var transaction = new PaymentTransaction
                {
                    SaleId = request.SaleId,
                    TransactionId = result.TransactionId,
                    CheckoutId = result.CheckoutId,
                    Status = "pending",
                    Amount = request.Amount,
                    Currency = "ARS",
                    PaymentMethod = "card",
                    CreatedAt = DateTime.UtcNow
                };

                _context.PaymentTransactions.Add(transaction);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("✅ Checkout creado - TransactionId: {TransactionId}, URL: {CheckoutUrl}",
                    result.TransactionId, result.CheckoutUrl);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "❌ Error de configuración");
                return StatusCode(500, new { error = "Error de configuración del servidor", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al crear checkout");
                return StatusCode(500, new { error = "Error al procesar el pago", message = ex.Message });
            }
        }

        /// <summary>
        /// Webhook para notificaciones de Payway
        /// POST: api/Payway/webhook
        /// </summary>
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
        {
            try
            {
                // Leer el body raw
                Request.EnableBuffering();
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                Request.Body.Position = 0;

                _logger.LogInformation("🔔 Webhook recibido de Payway: {Body}", body);

                // Parsear notificación
                var notification = JsonSerializer.Deserialize<PaywayWebhookNotification>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (notification == null || string.IsNullOrEmpty(notification.SiteTransactionId))
                {
                    _logger.LogWarning("⚠️ Webhook inválido: payload mal formado");
                    return BadRequest(new { error = "Payload inválido" });
                }

                // Buscar transacción
                var transaction = await _context.PaymentTransactions
                    .Include(t => t.Sale)
                    .FirstOrDefaultAsync(t => t.TransactionId == notification.SiteTransactionId, cancellationToken);

                if (transaction == null)
                {
                    _logger.LogWarning("⚠️ Transacción no encontrada: {TransactionId}", notification.SiteTransactionId);
                    return NotFound(new { error = "Transacción no encontrada" });
                }

                // Actualizar estado
                var oldStatus = transaction.Status;
                transaction.Status = notification.Status?.ToLower() ?? transaction.Status;
                transaction.StatusDetail = notification.StatusDetail;
                transaction.UpdatedAt = DateTime.UtcNow;

                // Actualizar estado de la venta según el resultado
                switch (transaction.Status)
                {
                    case "approved":
                        transaction.CompletedAt = DateTime.UtcNow;
                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 1; // Aprobado
                        }
                        _logger.LogInformation("✅ Pago aprobado - TransactionId: {TransactionId}", transaction.TransactionId);
                        break;

                    case "rejected":
                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 2; // Rechazado
                        }
                        _logger.LogWarning("⚠️ Pago rechazado - TransactionId: {TransactionId}, Detalle: {Detail}",
                            transaction.TransactionId, transaction.StatusDetail);
                        break;

                    case "pending":
                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 0; // Pendiente
                        }
                        _logger.LogInformation("⏳ Pago pendiente - TransactionId: {TransactionId}", transaction.TransactionId);
                        break;
                }

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("💾 Estado actualizado de {OldStatus} a {NewStatus} - TransactionId: {TransactionId}",
                    oldStatus, transaction.Status, transaction.TransactionId);

                return Ok(new { received = true, status = transaction.Status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error procesando webhook");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Consultar estado de un pago
        /// GET: api/Payway/payment-status/{transactionId}
        /// </summary>
        [HttpGet("payment-status/{transactionId}")]
        public async Task<IActionResult> GetPaymentStatus(
            string transactionId,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(transactionId))
                {
                    return BadRequest(new { error = "TransactionId es requerido" });
                }

                var transaction = await _context.PaymentTransactions
                    .Include(t => t.Sale)
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId, cancellationToken);

                if (transaction == null)
                {
                    return NotFound(new { error = "Transacción no encontrada" });
                }

                return Ok(new
                {
                    Status = transaction.Status,
                    StatusDetail = transaction.StatusDetail,
                    Amount = transaction.Amount,
                    Currency = transaction.Currency,
                    TransactionId = transaction.TransactionId,
                    CheckoutId = transaction.CheckoutId,
                    SaleId = transaction.SaleId,
                    CreatedAt = transaction.CreatedAt,
                    UpdatedAt = transaction.UpdatedAt,
                    CompletedAt = transaction.CompletedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al consultar estado de pago");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}