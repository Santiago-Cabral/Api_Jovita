using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ForrajeriaJovitaAPI.DTOs.Payway;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services.Interfaces;

namespace ForrajeriaJovitaAPI.Services
{
    public class PaywayService : IPaywayService
    {
        private readonly HttpClient _httpClient;
        private readonly PaywayOptions _options;
        private readonly ILogger<PaywayService> _logger;

        public PaywayService(
            HttpClient httpClient,
            PaywayOptions options,
            ILogger<PaywayService> logger)
        {
            _httpClient = httpClient;
            _options = options;
            _logger = logger;

            _logger.LogInformation("🔧 [PAYWAY] Servicio inicializado");
            _logger.LogInformation("   API URL: {Url}", _options.ApiUrl);
            _logger.LogInformation("   Public Key: {Key}...",
                _options.PublicKey?.Substring(0, Math.Min(10, _options.PublicKey?.Length ?? 0)));
        }

        public async Task<CreateCheckoutResponse> CreateCheckoutAsync(
            CreateCheckoutRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("💳 [PAYWAY] Creando checkout - Sale: {SaleId}, Amount: ${Amount}",
                    request.SaleId, request.Amount);

                var transactionId = $"JOV_{DateTime.UtcNow:yyyyMMddHHmmss}_{request.SaleId}_{new Random().Next(1000, 9999)}";
                var amountInCents = (int)(request.Amount * 100);

                var payload = new
                {
                    site_transaction_id = transactionId,
                    token = "cybersource",
                    customer = new
                    {
                        id = SanitizeEmail(request.Customer?.Email ?? $"customer_{request.SaleId}"),
                        email = request.Customer?.Email ?? $"temp{request.SaleId}@forrajeriajovita.com"
                    },
                    payment_method_id = 1,
                    bin = "450799",
                    amount = amountInCents,
                    currency = "ARS",
                    installments = 1,
                    description = request.Description ?? $"Pedido #{request.SaleId} - Forrajería Jovita",
                    payment_type = "single",
                    sub_payments = new object[] { }
                };

                var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _logger.LogDebug("📦 [PAYWAY] Payload: {Payload}", jsonPayload);

                // ✅ USAR URL COMPLETA, NO RELATIVA
                var url = $"{_options.ApiUrl}/payments";
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                httpRequest.Headers.Add("apikey", _options.PublicKey);
                httpRequest.Headers.Add("Cache-Control", "no-cache");

                _logger.LogInformation("🌐 [PAYWAY] POST {Url}", url);

                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogInformation("📊 [PAYWAY] Response: {Status}", (int)response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ [PAYWAY] Error: {Response}", responseContent);
                    throw new HttpRequestException($"Decidir error {response.StatusCode}: {responseContent}");
                }

                _logger.LogDebug("📄 [PAYWAY] Response body: {Response}", responseContent);

                var paywayResponse = JsonSerializer.Deserialize<PaywayPaymentResponse>(
                    responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (paywayResponse?.Id == null)
                {
                    _logger.LogError("❌ [PAYWAY] Sin Payment ID");
                    throw new InvalidOperationException("Decidir no devolvió Payment ID válido");
                }

                // Construir URL del formulario
                var baseUrl = _options.ApiUrl.Replace("/api/v2", "");
                var checkoutUrl = $"{baseUrl}/web/forms?payment_id={paywayResponse.Id}&apikey={_options.PublicKey}";

                _logger.LogInformation("✅ [PAYWAY] Checkout OK - ID: {Id}, URL: {Url}",
                    paywayResponse.Id, checkoutUrl);

                return new CreateCheckoutResponse
                {
                    CheckoutUrl = checkoutUrl,
                    CheckoutId = paywayResponse.Id.ToString(),
                    TransactionId = transactionId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [PAYWAY] Error");
                throw;
            }
        }

        private string SanitizeEmail(string email)
        {
            return email
                .Replace("@", "_at_")
                .Replace(".", "_")
                .Replace(" ", "_")
                .Replace("+", "_");
        }
    }
   
}
