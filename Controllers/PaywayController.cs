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

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
        {
            try
            {
                Request.EnableBuffering();
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                Request.Body.Position = 0;

                _logger.LogInformation("🔔 Webhook recibido de Payway: {Body}", body);

                PaywayWebhookNotification? notification;
                try
                {
                    notification = JsonSerializer.Deserialize<PaywayWebhookNotification>(body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "❌ Error al parsear webhook JSON");
                    return BadRequest(new { error = "JSON inválido" });
                }

                if (notification == null || string.IsNullOrEmpty(notification.SiteTransactionId))
                {
                    _logger.LogWarning("⚠️ Webhook inválido: payload mal formado");
                    return BadRequest(new { error = "Payload inválido" });
                }

                var transaction = await _context.PaymentTransactions
                    .Include(t => t.Sale)
                    .FirstOrDefaultAsync(t => t.TransactionId == notification.SiteTransactionId, cancellationToken);

                if (transaction == null)
                {
                    _logger.LogWarning("⚠️ Transacción no encontrada: {TransactionId}", notification.SiteTransactionId);
                    return NotFound(new { error = "Transacción no encontrada" });
                }

                if (transaction.Status == "approved" && notification.Status?.ToLower() == "approved")
                {
                    _logger.LogInformation("ℹ️ Transacción ya estaba aprobada, ignorando notificación duplicada");
                    return Ok(new { received = true, status = "already_processed" });
                }

                var oldStatus = transaction.Status;
                transaction.Status = notification.Status?.ToLower() ?? transaction.Status;
                transaction.StatusDetail = notification.StatusDetail;
                transaction.UpdatedAt = DateTime.UtcNow;

                switch (transaction.Status)
                {
                    case "approved":
                        transaction.CompletedAt = DateTime.UtcNow;
                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 1;
                        }
                        _logger.LogInformation("✅ Pago aprobado - TransactionId: {TransactionId}, SaleId: {SaleId}",
                            transaction.TransactionId, transaction.SaleId);
                        break;

                    case "rejected":
                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 2;
                        }
                        _logger.LogWarning("⚠️ Pago rechazado - TransactionId: {TransactionId}, Detalle: {Detail}",
                            transaction.TransactionId, transaction.StatusDetail);
                        break;

                    case "pending":
                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 0;
                        }
                        _logger.LogInformation("⏳ Pago pendiente - TransactionId: {TransactionId}", transaction.TransactionId);
                        break;

                    default:
                        _logger.LogWarning("⚠️ Estado desconocido: {Status}", transaction.Status);
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