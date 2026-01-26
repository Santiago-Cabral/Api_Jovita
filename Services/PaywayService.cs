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

            _logger.LogInformation("💳 Payway inicializado | ApiUrl: {Url}", _httpClient.BaseAddress);
        }

        public async Task<CreateCheckoutResponse> CreateCheckoutAsync(
            CreateCheckoutRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "💳 Creando checkout | SaleId={SaleId} Amount={Amount}",
                request.SaleId, request.Amount
            );

            var transactionId =
                $"JOV_{DateTime.UtcNow:yyyyMMddHHmmss}_{request.SaleId}_{Random.Shared.Next(1000, 9999)}";

            var payload = new
            {
                site_transaction_id = transactionId,
                token = "cybersource",
                customer = new
                {
                    id = Sanitize(request.Customer!.Email),
                    email = request.Customer.Email
                },
                payment_method_id = 1,
                bin = "450799",
                amount = (int)(request.Amount * 100),
                currency = "ARS",
                installments = 1,
                description = request.Description ?? $"Pedido #{request.SaleId}",
                payment_type = "single"
            };

            var json = JsonSerializer.Serialize(payload);

            var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v2/payments" // ✅ ENDPOINT CORRECTO
            )
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            requestMessage.Headers.Add("apikey", _options.PublicKey);

            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Payway error {Status}: {Body}", response.StatusCode, body);
                throw new HttpRequestException(body);
            }

            var paywayResponse = JsonSerializer.Deserialize<PaywayPaymentResponse>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (paywayResponse?.Id == null)
                throw new Exception("Payway no devolvió payment_id");

            var checkoutUrl =
                $"https://developers.decidir.com/web/forms?payment_id={paywayResponse.Id}&apikey={_options.PublicKey}";

            return new CreateCheckoutResponse
            {
                CheckoutId = paywayResponse.Id.ToString(),
                TransactionId = transactionId,
                CheckoutUrl = checkoutUrl
            };
        }

        private static string Sanitize(string value) =>
            value.Replace("@", "_").Replace(".", "_");
    }
}
