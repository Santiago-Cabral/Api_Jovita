using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ForrajeriaJovitaAPI.DTOs.Payway;
using ForrajeriaJovitaAPI.Services.Interfaces; // ADD THIS
using ForrajeriaJovitaAPI.Models.DTOs; // <--- Línea clave

namespace ForrajeriaJovitaAPI.Services
{
    // DELETE THE interface IPaywayService definition from here completely.
    // We will use the one in Services/Interfaces/IPaywayService.cs

    /// <summary>
    /// Servicio para integración con Decidir (Payway)
    /// </summary>
    public class PaywayService : IPaywayService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaywayService> _logger;

        private readonly string _apiBaseUrl;
        private readonly string _publicApiKey;
        private readonly string _privateApiKey; // Kept in case you need it later
        private readonly bool _isProduction;

        public PaywayService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<PaywayService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _isProduction = _configuration["Payway:Environment"]?.ToLower() == "production";
            _publicApiKey = _configuration["Payway:PublicApiKey"]
                ?? throw new InvalidOperationException("Payway:PublicApiKey no configurado");
            _privateApiKey = _configuration["Payway:PrivateApiKey"]
                ?? throw new InvalidOperationException("Payway:PrivateApiKey no configurado");

            _apiBaseUrl = _isProduction
                ? "https://live.decidir.com/api/v2"
                : "https://developers.decidir.com/api/v2";

            _logger.LogInformation("🔧 [PAYWAY] Inicializado - Ambiente: {Environment}, URL: {Url}",
                _isProduction ? "PRODUCCIÓN" : "SANDBOX", _apiBaseUrl);
        }

        public async Task<CreateCheckoutResponse> CreateCheckoutAsync(
            CreateCheckoutRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("💳 [PAYWAY] Iniciando creación de pago - SaleId: {SaleId}, Amount: ${Amount}",
                    request.SaleId, request.Amount);

                var transactionId = GenerateTransactionId(request.SaleId);
                var amountInCents = (int)(request.Amount * 100);

                var payload = new
                {
                    site_transaction_id = transactionId,
                    token = "cybersource",
                    customer = new
                    {
                        id = request.Customer?.Email?.Replace("@", "_").Replace(".", "_")
                            ?? $"customer_{request.SaleId}",
                        email = request.Customer?.Email ?? $"temp{request.SaleId}@example.com"
                    },
                    payment_method_id = 1,
                    bin = "450799",
                    amount = amountInCents,
                    currency = "ARS",
                    installments = 1,
                    description = request.Description ?? $"Pedido #{request.SaleId} - Forrajería Jovita",
                    payment_type = "single",
                    sub_payments = new object[] { },
                    fraud_detection = new
                    {
                        send_to_cs = false,
                        channel = "Web"
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_apiBaseUrl}/payments")
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                httpRequest.Headers.Add("apikey", _publicApiKey);
                httpRequest.Headers.Add("Cache-Control", "no-cache");

                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ [PAYWAY] Error HTTP {StatusCode}: {Content}", response.StatusCode, responseContent);
                    throw new HttpRequestException($"Payway error {response.StatusCode}: {responseContent}");
                }

                // FIX: Handle possible null deserialization
                var paywayResponse = JsonSerializer.Deserialize<PaywayPaymentResponse>(
                    responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (paywayResponse == null || paywayResponse.Id == null)
                {
                    throw new InvalidOperationException("Payway no devolvió un ID de pago válido");
                }

                var checkoutUrl = _isProduction
                    ? $"https://live.decidir.com/web/forms?payment_id={paywayResponse.Id}&apikey={_publicApiKey}"
                    : $"https://developers.decidir.com/web/forms?payment_id={paywayResponse.Id}&apikey={_publicApiKey}";

                return new CreateCheckoutResponse
                {
                    CheckoutUrl = checkoutUrl,
                    CheckoutId = paywayResponse.Id.ToString()!,
                    TransactionId = transactionId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [PAYWAY] Error procesando pago");
                throw;
            }
        }

        // Method required by the Interface, added stubs to compile
        public Task<PaymentStatusResponse?> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            // TODO: Implement Logic later
            return Task.FromResult<PaymentStatusResponse?>(null);
        }

        private string GenerateTransactionId(int saleId)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"JOV_{timestamp}_{saleId}_{random}";
        }
    }

    // --- DTO CLASSES ---
    // Ideally move these to their own files in the DTOs folder
    public class PaywayPaymentResponse
    {
        public int? Id { get; set; }
        public string? Status { get; set; }
    }
}