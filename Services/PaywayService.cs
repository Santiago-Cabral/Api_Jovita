// Services/PaywayService.cs
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
        private readonly string _siteId;

        public PaywayService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<PaywayService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("payway");
            _configuration = configuration;
            _logger = logger;

            // Leer credenciales (desde User Secrets en desarrollo)
            _privateKey = configuration["Payway:PrivateKey"]
                ?? throw new InvalidOperationException("Payway:PrivateKey no configurado");
            _siteId = configuration["Payway:SiteId"]
                ?? throw new InvalidOperationException("Payway:SiteId no configurado");
        }

        public async Task<PaywayCheckoutResponse> CreatePaymentAsync(PaywayCheckoutRequest request)
        {
            try
            {
                _logger.LogInformation("🔵 Creando pago Payway Forms - Amount: {Amount}, Email: {Email}",
                    request.Amount, request.Customer?.Email);

                // Generar ID de transacción único
                var transactionId = $"JOV_{DateTime.Now:yyyyMMddHHmmss}_{request.SaleId}";

                // Payload para Payway Ventas Online (Forms)
                var payload = new
                {
                    amount = (int)(request.Amount * 100), // Convertir a centavos
                    currency = "ARS",
                    description = request.Description ?? $"Pedido #{request.SaleId} - Forrajería Jovita",
                    customer = new
                    {
                        email = request.Customer?.Email ?? "cliente@temp.com",
                        name = request.Customer?.Name ?? "Cliente"
                    },
                    site_id = _siteId,
                    site_transaction_id = transactionId,
                    payment_type = "single",
                    back_url = request.CancelUrl,
                    success_url = request.ReturnUrl,
                    failure_url = request.CancelUrl
                };

                _logger.LogDebug("📤 Payload Payway: {Payload}", JsonSerializer.Serialize(payload));

                // Configurar headers
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _privateKey);

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                // Llamar a Payway API
                var response = await _httpClient.PostAsync("/v1.2/forms/validate", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("📥 Respuesta Payway: Status={Status}, Body={Body}",
                    response.StatusCode, responseBody);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ Error Payway: {Status} - {Body}", response.StatusCode, responseBody);
                    throw new Exception($"Error de Payway: {response.StatusCode} - {responseBody}");
                }

                // Parsear respuesta
                var paywayResponse = JsonSerializer.Deserialize<PaywayFormsResponse>(responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (paywayResponse?.Hash == null)
                {
                    _logger.LogError("❌ Respuesta inválida de Payway (sin hash): {Body}", responseBody);
                    throw new Exception("Payway no devolvió un hash válido");
                }

                // Construir URL del formulario
                var checkoutUrl = $"https://api.decidir.com/web/form?hash={paywayResponse.Hash}";

                _logger.LogInformation("✅ Checkout creado exitosamente - Hash: {Hash}", paywayResponse.Hash);

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

    // DTO interno para la respuesta de Payway Forms
    internal class PaywayFormsResponse
    {
        public string? Hash { get; set; }
        public string? Status { get; set; }
    }
}
