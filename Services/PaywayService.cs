using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ForrajeriaJovitaAPI.DTOs.Payway;

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
                ?? throw new InvalidOperationException("Payway:PrivateKey no configurado");
            _publicKey = configuration["Payway:PublicKey"]
                ?? throw new InvalidOperationException("Payway:PublicKey no configurado");
            _siteId = configuration["Payway:SiteId"]
                ?? throw new InvalidOperationException("Payway:SiteId no configurado");
            _isSandbox = configuration.GetValue<bool>("Payway:IsSandbox", true);
        }

        public async Task<PaywayCheckoutResponse> CreatePaymentAsync(PaywayCheckoutRequest request)
        {
            try
            {
                _logger.LogInformation("🔵 Creando pago Payway Forms - Amount: {Amount}, SaleId: {SaleId}",
                    request.Amount, request.SaleId);

                var transactionId = $"JOV_{DateTime.Now:yyyyMMddHHmmss}_{request.SaleId}";

                var payload = new
                {
                    site_id = _siteId,
                    site_transaction_id = transactionId,
                    amount = (decimal)(request.Amount * 100),
                    currency = "ARS",
                    description = request.Description ?? $"Pedido #{request.SaleId} - Forrajería Jovita",
                    customer = new
                    {
                        email = request.Customer?.Email ?? "cliente@temp.com",
                        name = request.Customer?.Name ?? "Cliente"
                    },
                    payment_type = "single",
                    back_url = request.CancelUrl,
                    success_url = request.ReturnUrl,
                    failure_url = request.CancelUrl
                };

                _logger.LogDebug("📤 Payload Payway: {Payload}", JsonSerializer.Serialize(payload));

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _privateKey);

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync("/v1.2/forms/validate", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("📥 Respuesta Payway: Status={Status}, Body={Body}",
                    response.StatusCode, responseBody);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ Error Payway: {Status} - {Body}", response.StatusCode, responseBody);
                    throw new Exception($"Error de Payway: {response.StatusCode} - {responseBody}");
                }

                var paywayResponse = JsonSerializer.Deserialize<PaywayFormsResponse>(responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (paywayResponse?.Hash == null)
                {
                    _logger.LogError("❌ Respuesta inválida de Payway (sin hash): {Body}", responseBody);
                    throw new Exception("Payway no devolvió un hash válido");
                }

                var baseUrl = _isSandbox
                    ? "https://forms.decidir.com"
                    : "https://ventasonline.payway.com.ar";

                var checkoutUrl = $"{baseUrl}/web/forms/{paywayResponse.Hash}?apikey={_publicKey}";

                _logger.LogInformation("✅ Checkout creado - Hash: {Hash}, URL: {Url}",
                    paywayResponse.Hash, checkoutUrl);

                return new PaywayCheckoutResponse
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
    }

    internal class PaywayFormsResponse
    {
        public string? Hash { get; set; }
        public string? Status { get; set; }
    }
}