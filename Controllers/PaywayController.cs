// Controllers/PaywayController.cs
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ForrajeriaJovitaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaywayController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaywayController> _logger;
        private readonly HttpClient _httpClient;
        private readonly ForrajeriaContext _context;

        public PaywayController(
            IConfiguration configuration,
            ILogger<PaywayController> logger,
            IHttpClientFactory httpClientFactory,
            ForrajeriaContext context)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _context = context;
        }

        /// <summary>
        /// Crear checkout en Payway
        /// POST: api/payway/create-checkout
        /// </summary>
        [HttpPost("create-checkout")]
        public async Task<IActionResult> CreateCheckout([FromBody] PaywayCheckoutRequest request)
        {
            try
            {
                _logger.LogInformation("📤 Creando checkout Payway para Sale ID: {SaleId}", request.SaleId);

                // Validaciones
                if (request.SaleId <= 0 || request.Amount <= 0)
                {
                    return BadRequest(new { error = "Datos incompletos" });
                }

                // Obtener configuración de Payway
                var publicKey = _configuration["Payway:PublicKey"];
                var privateKey = _configuration["Payway:PrivateKey"];
                var apiUrl = _configuration["Payway:ApiUrl"];

                if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
                {
                    _logger.LogError("❌ Credenciales de Payway no configuradas");
                    return StatusCode(500, new { error = "Credenciales de Payway no configuradas" });
                }

                // Generar ID único de transacción
                var transactionId = $"TXN-{request.SaleId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

                // Crear la firma (hash MD5)
                var dataToSign = $"{transactionId}{request.Amount}{privateKey}";
                var signature = GenerateMD5Hash(dataToSign);

                // Preparar payload para Payway
                var paywayPayload = new
                {
                    site_transaction_id = transactionId,
                    token = publicKey,
                    payment_method_id = 1, // 1 = Tarjeta de crédito
                    bin = (string?)null,
                    amount = request.Amount,
                    currency = "ARS",
                    installments = 1,
                    description = request.Description ?? $"Pedido #{request.SaleId} - Forrajería Jovita",
                    payment_type = "single",
                    sub_payments = new object[] { },
                    customer = new
                    {
                        id = request.Customer?.Phone ?? "guest",
                        email = request.Customer?.Email ?? $"{request.Customer?.Phone ?? "guest"}@temp.com",
                        name = request.Customer?.Name ?? "Cliente Web",
                        identification = new
                        {
                            type = "dni",
                            number = "00000000"
                        }
                    },
                    return_url = request.ReturnUrl ?? $"{_configuration["AppUrl"]}/payment/success",
                    cancel_url = request.CancelUrl ?? $"{_configuration["AppUrl"]}/payment/cancel",
                    fraud_detection = new
                    {
                        send_to_cs = false,
                        channel = "Web"
                    },
                    signature = signature
                };

                _logger.LogInformation("📦 Payload Payway: {Payload}", JsonSerializer.Serialize(paywayPayload));

                // Hacer la petición a Payway
                var content = new StringContent(
                    JsonSerializer.Serialize(paywayPayload),
                    Encoding.UTF8,
                    "application/json"
                );

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("apikey", publicKey);

                var response = await _httpClient.PostAsync($"{apiUrl}/v1/checkouts", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ Error de Payway: {Response}", responseBody);
                    return BadRequest(new
                    {
                        error = "Error al crear el checkout",
                        details = responseBody
                    });
                }

                var paywayResponse = JsonSerializer.Deserialize<PaywayCheckoutResponse>(responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _logger.LogInformation("✅ Checkout creado: {CheckoutId}", paywayResponse?.Id);

                // Guardar la transacción en la base de datos
                var transaction = new PaymentTransaction
                {
                    SaleId = request.SaleId,
                    TransactionId = transactionId,
                    CheckoutId = paywayResponse?.Id,
                    Status = "pending",
                    Amount = request.Amount,
                    Currency = "ARS",
                    PaymentMethod = "card",
                    CreatedAt = DateTime.UtcNow
                };

                _context.PaymentTransactions.Add(transaction);
                await _context.SaveChangesAsync();

                _logger.LogInformation("💾 Transacción guardada en BD: {TransactionId}", transactionId);

                return Ok(new
                {
                    CheckoutUrl = paywayResponse?.CheckoutUrl ?? paywayResponse?.Url,
                    CheckoutId = paywayResponse?.Id,
                    TransactionId = transactionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al crear checkout");
                return StatusCode(500, new
                {
                    error = "Error interno del servidor",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Webhook para recibir notificaciones de Payway
        /// POST: api/payway/webhook
        /// </summary>
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook([FromBody] PaywayWebhookNotification notification)
        {
            try
            {
                _logger.LogInformation("🔔 Notificación de Payway: {Notification}",
                    JsonSerializer.Serialize(notification));

                // Verificar la firma (importante para seguridad)
                var receivedSignature = Request.Headers["x-payway-signature"].ToString();
                var privateKey = _configuration["Payway:PrivateKey"];

                if (!string.IsNullOrEmpty(receivedSignature) && !string.IsNullOrEmpty(privateKey))
                {
                    var expectedSignature = GenerateMD5Hash(
                        JsonSerializer.Serialize(notification) + privateKey
                    );

                    if (receivedSignature != expectedSignature)
                    {
                        _logger.LogError("❌ Firma inválida en webhook");
                        return Unauthorized(new { error = "Firma inválida" });
                    }
                }

                // Buscar la transacción
                var transaction = await _context.PaymentTransactions
                    .Include(t => t.Sale)
                    .FirstOrDefaultAsync(t => t.TransactionId == notification.SiteTransactionId);

                if (transaction == null)
                {
                    _logger.LogWarning("⚠️ Transacción no encontrada: {TransactionId}",
                        notification.SiteTransactionId);
                    return NotFound(new { error = "Transacción no encontrada" });
                }

                // Actualizar estado de la transacción
                transaction.Status = notification.Status?.ToLower() ?? "unknown";
                transaction.StatusDetail = notification.StatusDetail;
                transaction.UpdatedAt = DateTime.UtcNow;

                // Procesar según el estado del pago
                switch (transaction.Status)
                {
                    case "approved":
                        _logger.LogInformation("✅ Pago aprobado: {TransactionId}", transaction.TransactionId);

                        transaction.CompletedAt = DateTime.UtcNow;

                        // Actualizar estado de la venta
                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 1; // 1 = Pagado
                            _logger.LogInformation("📝 Venta {SaleId} marcada como pagada", transaction.SaleId);
                        }

                        break;

                    case "rejected":
                        _logger.LogInformation("❌ Pago rechazado: {TransactionId}", transaction.TransactionId);

                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 2; // 2 = Rechazado
                        }
                        break;

                    case "pending":
                        _logger.LogInformation("⏳ Pago pendiente: {TransactionId}", transaction.TransactionId);

                        if (transaction.Sale != null)
                        {
                            transaction.Sale.PaymentStatus = 0; // 0 = Pendiente
                        }
                        break;

                    default:
                        _logger.LogInformation("❓ Estado desconocido: {Status}", notification.Status);
                        break;
                }

                await _context.SaveChangesAsync();

                // Siempre responder 200 OK a Payway
                return Ok(new { received = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error procesando webhook");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Verificar el estado de un pago
        /// GET: api/payway/payment-status/{transactionId}
        /// </summary>
        [HttpGet("payment-status/{transactionId}")]
        public async Task<IActionResult> GetPaymentStatus(string transactionId)
        {
            try
            {
                // Primero buscar en nuestra base de datos
                var transaction = await _context.PaymentTransactions
                    .Include(t => t.Sale)
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

                if (transaction == null)
                {
                    return NotFound(new { error = "Transacción no encontrada" });
                }

                // Opcionalmente, verificar con Payway
                var publicKey = _configuration["Payway:PublicKey"];
                var apiUrl = _configuration["Payway:ApiUrl"];

                if (!string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(apiUrl))
                {
                    try
                    {
                        _httpClient.DefaultRequestHeaders.Clear();
                        _httpClient.DefaultRequestHeaders.Add("apikey", publicKey);

                        var response = await _httpClient.GetAsync($"{apiUrl}/v1/payments/{transactionId}");

                        if (response.IsSuccessStatusCode)
                        {
                            var responseBody = await response.Content.ReadAsStringAsync();
                            var payment = JsonSerializer.Deserialize<PaywayPaymentStatus>(responseBody,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            // Actualizar estado si cambió
                            if (payment?.Status != null && payment.Status != transaction.Status)
                            {
                                transaction.Status = payment.Status;
                                transaction.StatusDetail = payment.StatusDetail;
                                transaction.UpdatedAt = DateTime.UtcNow;
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ No se pudo verificar con Payway: {Error}", ex.Message);
                    }
                }

                return Ok(new
                {
                    Status = transaction.Status,
                    StatusDetail = transaction.StatusDetail,
                    Amount = transaction.Amount,
                    TransactionId = transaction.TransactionId,
                    SaleId = transaction.SaleId,
                    CreatedAt = transaction.CreatedAt,
                    CompletedAt = transaction.CompletedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al consultar estado");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Generar hash MD5
        /// </summary>
        private string GenerateMD5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);

                var sb = new StringBuilder();
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }

    #region DTOs

    public class PaywayCheckoutRequest
    {
        public int SaleId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public CustomerInfo? Customer { get; set; }
        public string? ReturnUrl { get; set; }
        public string? CancelUrl { get; set; }
    }

    public class CustomerInfo
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    public class PaywayCheckoutResponse
    {
        public string? Id { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? Url { get; set; }
    }

    public class PaywayWebhookNotification
    {
        public string? SiteTransactionId { get; set; }
        public string? Status { get; set; }
        public string? StatusDetail { get; set; }
        public decimal Amount { get; set; }
    }

    public class PaywayPaymentStatus
    {
        public string? Status { get; set; }
        public string? StatusDetail { get; set; }
        public decimal Amount { get; set; }
        public string? SiteTransactionId { get; set; }
    }

    #endregion
}