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
        }

        // ======================================================
        // CREATE CHECKOUT
        // ======================================================
        public async Task<CreateCheckoutResponse> CreateCheckoutAsync(
            CreateCheckoutRequest request,
            CancellationToken cancellationToken = default)
        {
            var transactionId = $"JOV_{DateTime.UtcNow:yyyyMMddHHmmss}_{request.SaleId}";
            var amountInCents = (int)(request.Amount * 100);

            var payload = new
            {
                site_transaction_id = transactionId,
                token = "cybersource",
                customer = new
                {
                    id = SanitizeEmail(request.Customer.Email),
                    email = request.Customer.Email
                },
                payment_method_id = 1,
                bin = "450799",
                amount = amountInCents,
                currency = "ARS",
                installments = 1,
                description = request.Description,
                payment_type = "single",
                sub_payments = Array.Empty<object>()
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "payments")
            {
                Content = content
            };

            httpRequest.Headers.Add("apikey", _options.PublicKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PAYWAY ERROR: {Body}", body);
                throw new Exception("Error al crear el checkout de pago");
            }

            var paywayResponse = JsonSerializer.Deserialize<PaywayPaymentResponse>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (paywayResponse == null || paywayResponse.Id == null)
                throw new Exception("Respuesta inválida de Payway");

            var checkoutUrl =
                $"{_options.ApiUrl.Replace("/api/v2", "")}/web/forms" +
                $"?payment_id={paywayResponse.Id}&apikey={_options.PublicKey}";

            return new CreateCheckoutResponse
            {
                CheckoutId = paywayResponse.Id.ToString(),
                CheckoutUrl = checkoutUrl,
                TransactionId = transactionId
            };
        }

        // ======================================================
        // PAYMENT STATUS
        // ======================================================
        public async Task<PaymentStatusResponse?> GetPaymentStatusAsync(
            string paymentId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync(
                $"payments/{paymentId}",
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return JsonSerializer.Deserialize<PaymentStatusResponse>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }

        private string SanitizeEmail(string email)
        {
            return email
                .Replace("@", "_at_")
                .Replace(".", "_")
                .Replace("+", "_")
                .Replace(" ", "_");
        }
    }
}
