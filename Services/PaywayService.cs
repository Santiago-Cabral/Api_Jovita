using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ForrajeriaJovitaAPI.DTOs.Payway;
using ForrajeriaJovitaAPI.Services.Interfaces;

namespace ForrajeriaJovitaAPI.Services
{
    public class PaywayService : IPaywayService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaywayService> _logger;
        private readonly string _privateKey;
        private readonly string _publicKey;
        private readonly string _siteId;
        private readonly bool _isSandbox;

        public PaywayService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<PaywayService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("payway");
            _configuration = configuration;
            _logger = logger;

            _privateKey = configuration["Payway:PrivateKey"]
                ?? throw new InvalidOperationException("Payway:PrivateKey no configurado en User Secrets");
            _publicKey = configuration["Payway:PublicKey"]
                ?? throw new InvalidOperationException("Payway:PublicKey no configurado en User Secrets");
            _siteId = configuration["Payway:SiteId"]
                ?? throw new InvalidOperationException("Payway:SiteId no configurado en User Secrets");
            _isSandbox = configuration.GetValue<bool>("Payway:IsSandbox", true);

            _logger.LogInformation("🔧 PaywayService inicializado - Sandbox: {IsSandbox}, SiteId: {SiteId}, BaseAddress: {Base}",
                _isSandbox, _siteId, _httpClient.BaseAddress);
        }

        public async Task<CheckoutResponse> CreateCheckoutAsync(
            CreateCheckoutRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("💳 Creando checkout Payway - SaleId: {SaleId}, Amount: {Amount}",
                    request.SaleId, request.Amount);

                var transactionId = $"JOV_{DateTime.UtcNow:yyyyMMddHHmmss}_{request.SaleId}";

                // Nota: Payway espera monto en centavos (int)
                var payload = new
                {
                    site_id = _siteId,
                    site_transaction_id = transactionId,
                    amount = (int)(request.Amount * 100m),
                    currency = "ARS",
                    description = request.Description ?? $"Pedido #{request.SaleId}",
                    customer = new
                    {
                        email = request.Customer?.Email ?? string.Empty,
                        name = request.Customer?.Name ?? string.Empty,
                        phone = request.Customer?.Phone ?? string.Empty
                    },
                    payment_type = "single",
                    success_url = request.ReturnUrl,
                    failure_url = request.CancelUrl,
                    back_url = request.CancelUrl
                };

                var jsonPayload = JsonSerializer.Serialize(payload);

                _logger.LogDebug("📤 Payload a Payway: {Payload}", jsonPayload);

                _httpClient.DefaultRequestHeaders.Clear();
                // Algunos entornos usan "apikey" y otros "x-api-key". Mandamos ambos para mayor compatibilidad.
                _httpClient.DefaultRequestHeaders.Add("apikey", _privateKey);
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _privateKey);

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // USAR EL PATH CORRECTO (forms validate) - relativo al BaseAddress del HttpClient
                var response = await _httpClient.PostAsync("/web/v1.2/forms/validate", content, cancellationToken);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogInformation("📥 Respuesta Payway: Status={Status}, Body={Body}",
                    response.StatusCode, responseBody);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ Error de Payway: {Status} - {Body}", response.StatusCode, responseBody);
                    throw new HttpRequestException($"Error al crear checkout en Payway: {response.StatusCode} - {responseBody}");
                }

                var paywayResponse = JsonSerializer.Deserialize<PaywayFormsResponse>(
                    responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (paywayResponse?.Hash == null)
                {
                    _logger.LogError("❌ Respuesta inválida de Payway (sin hash): {Body}", responseBody);
                    throw new InvalidOperationException("Payway no devolvió un hash válido");
                }

                // Construir la URL pública del formulario (dependiendo sandbox/producción)
                var checkoutHost = _isSandbox ? "https://forms.decidir.com" : "https://ventasonline.payway.com.ar";
                var checkoutUrl = $"{checkoutHost}/web/forms/{paywayResponse.Hash}?apikey={_publicKey}";

                _logger.LogInformation("✅ Checkout creado - Hash: {Hash}, URL: {Url}",
                    paywayResponse.Hash, checkoutUrl);

                return new CheckoutResponse
                {
                    CheckoutUrl = checkoutUrl,
                    CheckoutId = paywayResponse.Hash,
                    TransactionId = transactionId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error al crear checkout en Payway");
                throw;
            }
        }

        public async Task<PaymentStatusResponse?> GetPaymentStatusAsync(
            string transactionId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("🔍 Consultando estado de pago: {TransactionId}", transactionId);

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("apikey", _privateKey);
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _privateKey);

                // Endpoint de pagos (relativo). Si tu proveedor usa otro path, ajústalo en consecuencia.
                var response = await _httpClient.GetAsync($"/web/v1.2/payments/{transactionId}", cancellationToken);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ No se pudo obtener estado: {Status} - {Body}", response.StatusCode, responseBody);
                    return null;
                }

                var result = JsonSerializer.Deserialize<PaywayPaymentResponse>(
                    responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null) return null;

                return new PaymentStatusResponse
                {
                    Status = result.Status ?? "unknown",
                    StatusDetail = result.StatusDetail,
                    Amount = result.Amount ?? 0,
                    Currency = result.Currency ?? "ARS",
                    TransactionId = transactionId,
                    PaymentId = result.PaymentId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al consultar estado de pago");
                return null;
            }
        }

        private class PaywayFormsResponse
        {
            public string? Hash { get; set; }
            public string? Status { get; set; }
        }

        private class PaywayPaymentResponse
        {
            public string? Status { get; set; }
            public string? StatusDetail { get; set; }
            public decimal? Amount { get; set; }
            public string? Currency { get; set; }
            public string? PaymentId { get; set; }
        }
    }
}
