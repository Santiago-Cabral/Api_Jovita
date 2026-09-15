using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ForrajeriaJovitaAPI.DTOs.MercadoPago;
using ForrajeriaJovitaAPI.Services.Interfaces;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;

namespace ForrajeriaJovitaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MercadoPagoController : ControllerBase
    {
        private readonly IMercadoPagoService _mpService;
        private readonly ILogger<MercadoPagoController> _logger;
        private readonly ForrajeriaContext _context;
        private readonly IConfiguration _configuration;

        public MercadoPagoController(
            IMercadoPagoService mpService,
            ILogger<MercadoPagoController> logger,
            ForrajeriaContext context,
            IConfiguration configuration)
        {
            _mpService = mpService;
            _logger = logger;
            _context = context;
            _configuration = configuration;
        }

        /// <summary>
        /// POST /api/MercadoPago/create-checkout
        /// Mismo contrato que Payway, para no romper el front.
        /// </summary>
        [HttpPost("create-checkout")]
        public async Task<IActionResult> CreateCheckout(
            [FromBody] CreateCheckoutRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                if (request.Amount <= 0)
                    return BadRequest(new { error = "El monto debe ser mayor a cero" });

                if (request.Customer == null || string.IsNullOrEmpty(request.Customer.Email))
                    return BadRequest(new { error = "Email del cliente es requerido" });

                var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);
                if (sale == null)
                    return NotFound(new { error = $"Venta #{request.SaleId} no encontrada" });

                var checkoutResponse = await _mpService.CreateCheckoutAsync(request, cancellationToken);

                var transaction = new PaymentTransaction
                {
                    SaleId = request.SaleId,
                    TransactionId = checkoutResponse.TransactionId,
                    CheckoutId = checkoutResponse.CheckoutId,
                    Amount = request.Amount,
                    Currency = "ARS",
                    Status = "pending",
                    Provider = "mercadopago",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.PaymentTransactions.Add(transaction);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(new
                {
                    CheckoutUrl = checkoutResponse.CheckoutUrl,
                    CheckoutId = checkoutResponse.CheckoutId,
                    TransactionId = checkoutResponse.TransactionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [MP-CREATE-CHECKOUT] Error");
                return StatusCode(500, new { error = "Error al crear el checkout de pago", message = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/MercadoPago/webhook
        /// MP notifica solo type=payment y data.id. Hay que consultar el pago real.
        /// </summary>
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
        {
            try
            {
                Request.EnableBuffering();
                using var reader = new System.IO.StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                Request.Body.Position = 0;

                _logger.LogInformation("🔔 [MP-WEBHOOK] Body: {Body}", body);

                var signatureHeader = Request.Headers["x-signature"].FirstOrDefault();
                var requestId = Request.Headers["x-request-id"].FirstOrDefault();
                var dataIdFromQuery = Request.Query["data.id"].FirstOrDefault();

                var secret = _configuration["MercadoPago:WebhookSecret"];

                if (!string.IsNullOrEmpty(secret) && !string.IsNullOrEmpty(signatureHeader))
                {
                    var parts = signatureHeader.Split(',')
                        .Select(p => p.Split('='))
                        .Where(p => p.Length == 2)
                        .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

                    parts.TryGetValue("ts", out var ts);
                    parts.TryGetValue("v1", out var v1);

                    if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(v1))
                    {
                        _logger.LogWarning("⚠️ [MP-WEBHOOK] Firma mal formada");
                        return Unauthorized(new { error = "Invalid signature format" });
                    }

                    var manifest = $"id:{dataIdFromQuery};request-id:{requestId};ts:{ts};";
                    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                    var computedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));
                    var computedHex = Convert.ToHexString(computedBytes).ToLowerInvariant();

                    if (!string.Equals(computedHex, v1, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("❌ [MP-WEBHOOK] Firma inválida");
                        return Unauthorized(new { error = "Invalid signature" });
                    }

                    _logger.LogInformation("🔐 [MP-WEBHOOK] Firma válida");
                }
                else
                {
                    _logger.LogWarning("⚠️ [MP-WEBHOOK] Sin WebhookSecret configurado o sin header de firma — se procesa sin validar (solo recomendado en test)");
                }

                string? paymentId = dataIdFromQuery;
                if (string.IsNullOrEmpty(paymentId) && !string.IsNullOrEmpty(body))
                {
                    try
                    {
                        var notification = System.Text.Json.JsonSerializer.Deserialize<MercadoPagoWebhookNotification>(
                            body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        paymentId = notification?.Data?.Id;
                    }
                    catch { /* ignorar, puede ser otro tipo de evento */ }
                }

                if (string.IsNullOrEmpty(paymentId))
                {
                    _logger.LogInformation("ℹ️ [MP-WEBHOOK] Notificación sin payment id, se ignora");
                    return Ok(new { received = true });
                }

                var payment = await _mpService.GetPaymentAsync(paymentId, cancellationToken);

                if (payment == null || string.IsNullOrEmpty(payment.ExternalReference))
                {
                    _logger.LogWarning("⚠️ [MP-WEBHOOK] No se pudo obtener el pago o no tiene external_reference: {PaymentId}", paymentId);
                    return Ok(new { received = true });
                }

                var transaction = await _context.PaymentTransactions
                    .Include(t => t.Sale)
                    .FirstOrDefaultAsync(t => t.TransactionId == payment.ExternalReference, cancellationToken);

                if (transaction == null)
                {
                    _logger.LogWarning("⚠️ [MP-WEBHOOK] Transacción no encontrada: {Ref}", payment.ExternalReference);
                    return NotFound(new { error = "Transacción no encontrada" });
                }

                var newStatus = payment.Status?.ToLowerInvariant();
                if (!string.IsNullOrEmpty(newStatus) && transaction.Status == newStatus)
                {
                    return Ok(new { received = true, duplicated = true });
                }

                transaction.Status = newStatus ?? transaction.Status;
                transaction.StatusDetail = payment.StatusDetail;
                transaction.UpdatedAt = DateTime.UtcNow;

                switch (transaction.Status)
                {
                    case "approved":
                        transaction.CompletedAt = DateTime.UtcNow;
                        if (transaction.Sale != null) transaction.Sale.PaymentStatus = 1;
                        break;
                    case "rejected":
                        if (transaction.Sale != null) transaction.Sale.PaymentStatus = 2;
                        break;
                    case "cancelled":
                    case "refunded":
                        if (transaction.Sale != null) transaction.Sale.PaymentStatus = 3;
                        break;
                    default: // pending, in_process
                        if (transaction.Sale != null) transaction.Sale.PaymentStatus = 0;
                        break;
                }

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("💾 [MP-WEBHOOK] Estado actualizado: {Status} para {TransactionId}", transaction.Status, transaction.TransactionId);

                return Ok(new { received = true, status = transaction.Status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [MP-WEBHOOK] Error");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/MercadoPago/payment-status/{transactionId}
        /// Se consulta por NUESTRO transactionId (external_reference).
        /// </summary>
        [HttpGet("payment-status/{transactionId}")]
        public async Task<IActionResult> GetPaymentStatus(string transactionId, CancellationToken cancellationToken)
        {
            var transaction = await _context.PaymentTransactions
                .Include(t => t.Sale)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId, cancellationToken);

            if (transaction == null)
                return NotFound(new { error = "Transacción no encontrada" });

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

        [HttpGet("health")]
        public IActionResult HealthCheck() => Ok(new { status = "healthy", service = "mercadopago", timestamp = DateTime.UtcNow });
    }
}