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
using ForrajeriaJovitaAPI.Services.Interfaces;
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
        /// POST /api/Payway/create-checkout
        /// Crea un checkout de Payway para procesar el pago
        /// </summary>
        [HttpPost("create-checkout")]
        public async Task<IActionResult> CreateCheckout(
            [FromBody] CreateCheckoutRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("💳 [CREATE-CHECKOUT] Iniciando para venta #{SaleId} - Monto: ${Amount}",
                    request.SaleId, request.Amount);

                // Validaciones básicas
                if (request.Amount <= 0)
                {
                    _logger.LogWarning("⚠️ Monto inválido: {Amount}", request.Amount);
                    return BadRequest(new { error = "El monto debe ser mayor a cero" });
                }

                if (request.Customer == null ||
                    string.IsNullOrEmpty(request.Customer.Name) ||
                    string.IsNullOrEmpty(request.Customer.Email))
                {
                    _logger.LogWarning("⚠️ Datos de cliente incompletos");
                    return BadRequest(new { error = "Nombre y email del cliente son requeridos" });
                }

                // Verificar que la venta existe
                var sale = await _context.Sales
                    .FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

                if (sale == null)
                {
                    _logger.LogWarning("⚠️ Venta no encontrada: #{SaleId}", request.SaleId);
                    return NotFound(new { error = $"Venta #{request.SaleId} no encontrada" });
                }

                // Usar la nueva firma del servicio (recibe CreateCheckoutRequest)
                var checkoutResponse = await _paywayService.CreateCheckoutAsync(request, cancellationToken);

                _logger.LogInformation("✅ [CREATE-CHECKOUT] Checkout creado - CheckoutId: {CheckoutId}, TransactionId: {TransactionId}",
                    checkoutResponse.CheckoutId, checkoutResponse.TransactionId);

                // Guardar transacción en la base de datos
                var transaction = new PaymentTransaction
                {
                    SaleId = request.SaleId,
                    TransactionId = checkoutResponse.TransactionId,
                    CheckoutId = checkoutResponse.CheckoutId,
                    Amount = request.Amount,
                    Currency = "ARS",
                    Status = "pending",
                    Provider = "payway",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.PaymentTransactions.Add(transaction);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("💾 [CREATE-CHECKOUT] Transacción guardada en BD");

                // Responder con la URL del checkout
                return Ok(new
                {
                    CheckoutUrl = checkoutResponse.CheckoutUrl,
                    CheckoutId = checkoutResponse.CheckoutId,
                    TransactionId = checkoutResponse.TransactionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [CREATE-CHECKOUT] Error al crear checkout");
                return StatusCode(500, new
                {
                    error = "Error al crear el checkout de pago",
                    message = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// POST /api/Payway/webhook
        /// Recibe notificaciones de estado de pago desde Payway
        /// </summary>
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
        {
            try
            {
                // Leer el body del request
                Request.EnableBuffering();
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                Request.Body.Position = 0;

                _logger.LogInformation("🔔 [WEBHOOK] Notificación recibida: {Body}", body);

                // Parsear la notificación
                PaywayWebhookNotification? notification;
                try
                {
                    notification = JsonSerializer.Deserialize<PaywayWebhookNotification>(body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "❌ [WEBHOOK] Error al parsear JSON");
                    return BadRequest(new { error = "JSON inválido" });
                }

                if (notification == null || string.IsNullOrEmpty(notification.SiteTransactionId))
                {
                    _logger.LogWarning("⚠️ [WEBHOOK] Payload mal formado");
                    return BadRequest(new { error = "Payload inválido" });
                }

                // Buscar la transacción
                var transaction = await _context.PaymentTransactions
                    .Include(t => t.Sale)
                    .FirstOrDefaultAsync(
                        t => t.TransactionId == notification.SiteTransactionId,
                        cancellationToken);

                if (transaction == null)
                {
                    _logger.LogWarning("⚠️ [WEBHOOK] Transacción no encontrada: {TransactionId}",
                        notification.SiteTransactionId);
                    return NotFound(new { error = "Transacción no encontrada" });
                }

                // Evitar procesar notificaciones duplicadas
                if (transaction.Status == "approved" && notification.Status?.ToLower() == "approved")
                {
                    _logger.LogInformation("ℹ️ [WEBHOOK] Transacción ya aprobada, ignorando duplicado");
                    return Ok(new { received = true, status = "already_processed" });
                }

                // Actualizar estado de la transacción
                var oldStatus = transaction.Status;
                transaction.Status = notification.Status?.ToLower() ?? transaction.Status;
                transaction.StatusDetail = notification.StatusDetail;
                transaction.UpdatedAt = DateTime.UtcNow;

                // Actualizar estado del pago según el resultado
                switch (transaction.Status)
                {
                    case "approved":
                        transaction.CompletedAt = DateTime.UtcNow;
                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 1; // Pagado
                        }
                        _logger.LogInformation("✅ [WEBHOOK] Pago aprobado - TransactionId: {TransactionId}, SaleId: {SaleId}",
                            transaction.TransactionId, transaction.SaleId);
                        break;

                    case "rejected":
                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 2; // Rechazado
                        }
                        _logger.LogWarning("⚠️ [WEBHOOK] Pago rechazado - TransactionId: {TransactionId}, Detalle: {Detail}",
                            transaction.TransactionId, transaction.StatusDetail);
                        break;

                    case "pending":
                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 0; // Pendiente
                        }
                        _logger.LogInformation("⏳ [WEBHOOK] Pago pendiente - TransactionId: {TransactionId}",
                            transaction.TransactionId);
                        break;

                    default:
                        _logger.LogWarning("⚠️ [WEBHOOK] Estado desconocido: {Status}", transaction.Status);
                        break;
                }

                // Guardar cambios
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("💾 [WEBHOOK] Estado actualizado: {OldStatus} → {NewStatus}",
                    oldStatus, transaction.Status);

                return Ok(new { received = true, status = transaction.Status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [WEBHOOK] Error al procesar notificación");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/Payway/payment-status/{transactionId}
        /// Consulta el estado de un pago
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

                _logger.LogInformation("🔍 [PAYMENT-STATUS] Consultando: {TransactionId}", transactionId);

                var transaction = await _context.PaymentTransactions
                    .Include(t => t.Sale)
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId, cancellationToken);

                if (transaction == null)
                {
                    _logger.LogWarning("⚠️ [PAYMENT-STATUS] Transacción no encontrada: {TransactionId}",
                        transactionId);
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
                _logger.LogError(ex, "❌ [PAYMENT-STATUS] Error al consultar estado");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/Payway/health
        /// Health check del servicio de Payway
        /// </summary>
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                status = "healthy",
                service = "payway",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
