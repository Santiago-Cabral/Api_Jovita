// Controllers/PaywayController.cs
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
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
        private readonly IHttpClientFactory _httpFactory;
        private readonly ForrajeriaContext _context;

        public PaywayController(
            IConfiguration configuration,
            ILogger<PaywayController> logger,
            IHttpClientFactory httpClientFactory,
            ForrajeriaContext context)
        {
            _configuration = configuration;
            _logger = logger;
            _httpFactory = httpClientFactory;
            _context = context;
        }

        /// <summary>
        /// Crear checkout en Payway
        /// POST: api/payway/create-checkout
        /// </summary>
        [HttpPost("create-checkout")]
        public async Task<IActionResult> CreateCheckout([FromBody] PaywayCheckoutRequest request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("📤 Creando checkout Payway para Sale ID: {SaleId}", request.SaleId);

                if (request.SaleId <= 0 || request.Amount <= 0)
                    return BadRequest(new { error = "Datos incompletos" });

                var publicKey = _configuration["Payway:PublicKey"];
                var privateKey = _configuration["Payway:PrivateKey"];
                var apiUrl = _configuration["Payway:ApiUrl"]?.TrimEnd('/') ?? throw new Exception("Payway:ApiUrl no configurado");

                if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
                {
                    _logger.LogError("❌ Credenciales de Payway no configuradas");
                    return StatusCode(500, new { error = "Credenciales de Payway no configuradas" });
                }

                var transactionId = $"TXN-{request.SaleId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

                // 🔧 URLS DE RETORNO CORREGIDAS
                var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:5173";
                var returnUrl = request.ReturnUrl ?? $"{frontendUrl}/payment-success";
                var cancelUrl = request.CancelUrl ?? $"{frontendUrl}/payment-cancel";

                _logger.LogInformation("🔗 URLs configuradas - Success: {ReturnUrl}, Cancel: {CancelUrl}", returnUrl, cancelUrl);

                // signature method configurable: MD5 (legacy) o HMACSHA256 (preferible si la doc lo pide)
                var signatureMethod = (_configuration["Payway:SignatureMethod"] ?? "MD5").ToUpperInvariant();
                var dataToSign = $"{transactionId}{request.Amount}{privateKey}";

                string signature = signatureMethod switch
                {
                    "HMACSHA256" => ComputeHmacSha256Hex(dataToSign, privateKey),
                    _ => ComputeMd5Hex(dataToSign)
                };

                var payload = new
                {
                    site_transaction_id = transactionId,
                    token = publicKey,
                    payment_method_id = 1,
                    bin = (string?)null,
                    amount = request.Amount,
                    currency = "ARS",
                    installments = 1,
                    description = request.Description ?? $"Pedido #{request.SaleId} - Forrajeria Jovita",
                    payment_type = "single",
                    sub_payments = new object[] { },
                    customer = new
                    {
                        id = request.Customer?.Phone ?? "guest",
                        email = request.Customer?.Email ?? $"{request.Customer?.Phone ?? "guest"}@temp.com",
                        name = request.Customer?.Name ?? "Cliente Web",
                        identification = new { type = "dni", number = "00000000" }
                    },
                    return_url = returnUrl,
                    cancel_url = cancelUrl,
                    fraud_detection = new { send_to_cs = false, channel = "Web" },
                    signature = signature
                };

                _logger.LogDebug("📦 Payload Payway (masked): {Payload}", JsonSerializer.Serialize(payload));

                var http = _httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(30);

                // AUTH: según configuración
                var authType = (_configuration["Payway:AuthType"] ?? "ApiKey").ToLowerInvariant();
                if (authType == "basic")
                {
                    var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{privateKey}"));
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
                }
                else
                {
                    // ApiKey header
                    http.DefaultRequestHeaders.Remove("apikey");
                    http.DefaultRequestHeaders.Add("apikey", publicKey);
                }

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await http.PostAsync($"{apiUrl}/v1/checkouts", content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ Error de Payway: {Status} {Response}", response.StatusCode, responseBody);
                    return BadRequest(new { error = "Error al crear el checkout", details = responseBody });
                }

                var paywayResponse = JsonSerializer.Deserialize<PaywayCheckoutResponse>(responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (paywayResponse == null)
                {
                    _logger.LogError("❌ Respuesta inválida de Payway: {Response}", responseBody);
                    return StatusCode(500, new { error = "Respuesta inválida de Payway" });
                }

                // Guardar la transacción en la base de datos
                var transaction = new PaymentTransaction
                {
                    SaleId = request.SaleId,
                    TransactionId = transactionId,
                    CheckoutId = paywayResponse.Id,
                    Status = "pending",
                    Amount = request.Amount,
                    Currency = "ARS",
                    PaymentMethod = "card",
                    CreatedAt = DateTime.UtcNow
                };

                _context.PaymentTransactions.Add(transaction);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("💾 Transacción guardada en BD: {TransactionId}", transactionId);

                return Ok(new
                {
                    CheckoutUrl = paywayResponse.CheckoutUrl ?? paywayResponse.Url,
                    CheckoutId = paywayResponse.Id,
                    TransactionId = transactionId
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("⏱ Request cancelado por timeout o token");
                return StatusCode(408, new { error = "Request timeout" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al crear checkout");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Webhook para recibir notificaciones de Payway
        /// POST: api/payway/webhook
        /// </summary>
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
        {
            try
            {
                // Leemos el body raw para validar firma exactamente como lo envía Payway
                Request.EnableBuffering();
                using var sr = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var rawBody = await sr.ReadToEndAsync();
                Request.Body.Position = 0;

                _logger.LogInformation("🔔 Raw webhook body recibido: {Body}", rawBody);

                var privateKey = _configuration["Payway:PrivateKey"];
                var receivedSignature = Request.Headers["x-payway-signature"].ToString();

                if (!string.IsNullOrEmpty(receivedSignature) && !string.IsNullOrEmpty(privateKey))
                {
                    var signatureMethod = (_configuration["Payway:SignatureMethod"] ?? "MD5").ToUpperInvariant();
                    var expectedSignature = signatureMethod switch
                    {
                        "HMACSHA256" => ComputeHmacSha256Hex(rawBody + privateKey, privateKey),
                        _ => ComputeMd5Hex(rawBody + privateKey)
                    };

                    if (!string.Equals(receivedSignature, expectedSignature, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogError("❌ Firma inválida en webhook. Received: {Received} Expected: {Expected}", receivedSignature, expectedSignature);
                        return Unauthorized(new { error = "Firma inválida" });
                    }
                }

                var notification = JsonSerializer.Deserialize<PaywayWebhookNotification>(rawBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (notification == null)
                {
                    _logger.LogWarning("⚠️ Webhook: payload inválido");
                    return BadRequest(new { error = "Payload inválido" });
                }

                _logger.LogInformation("🔔 Notificación de Payway (parsed): {Notification}", JsonSerializer.Serialize(notification));

                var transaction = await _context.PaymentTransactions
                    .Include(t => t.Sale)
                    .FirstOrDefaultAsync(t => t.TransactionId == notification.SiteTransactionId, cancellationToken);

                if (transaction == null)
                {
                    _logger.LogWarning("⚠️ Transacción no encontrada: {TransactionId}", notification.SiteTransactionId);
                    return NotFound(new { error = "Transacción no encontrada" });
                }

                transaction.Status = notification.Status?.ToLower() ?? transaction.Status;
                transaction.StatusDetail = notification.StatusDetail;
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
                    case "pending":
                        if (transaction.Sale != null) transaction.Sale.PaymentStatus = 0;
                        break;
                }

                await _context.SaveChangesAsync(cancellationToken);

                // Responder 200 a Payway
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
        public async Task<IActionResult> GetPaymentStatus(string transactionId, CancellationToken cancellationToken)
        {
            try
            {
                var transaction = await _context.PaymentTransactions
                    .Include(t => t.Sale)
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId, cancellationToken);

                if (transaction == null)
                    return NotFound(new { error = "Transacción no encontrada" });

                var publicKey = _configuration["Payway:PublicKey"];
                var apiUrl = _configuration["Payway:ApiUrl"]?.TrimEnd('/');

                if (!string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(apiUrl))
                {
                    try
                    {
                        var http = _httpFactory.CreateClient();
                        http.Timeout = TimeSpan.FromSeconds(15);
                        http.DefaultRequestHeaders.Remove("apikey");
                        http.DefaultRequestHeaders.Add("apikey", publicKey);

                        var response = await http.GetAsync($"{apiUrl}/v1/payments/{transactionId}", cancellationToken);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                            var payment = JsonSerializer.Deserialize<PaywayPaymentStatus>(responseBody,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            if (payment?.Status != null && payment.Status != transaction.Status)
                            {
                                transaction.Status = payment.Status;
                                transaction.StatusDetail = payment.StatusDetail;
                                transaction.UpdatedAt = DateTime.UtcNow;
                                await _context.SaveChangesAsync(cancellationToken);
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

        #region Helpers

        private static string ComputeMd5Hex(string input)
        {
            using var md5 = MD5.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hash = md5.ComputeHash(inputBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static string ComputeHmacSha256Hex(string input, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            using var hmac = new HMACSHA256(keyBytes);
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hash = hmac.ComputeHash(inputBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        #endregion
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
