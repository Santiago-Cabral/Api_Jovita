using System;
using System.Net.Http;
using System.Security.Cryptography;
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

        private readonly string _apiBaseUrl;
        private readonly string _publicApiKey;
        private readonly string _privateApiKey;
        private readonly string _siteId;
        private readonly bool _isProduction;

        public PaywayService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<PaywayService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _isProduction = string.Equals(
                _configuration["Payway:Environment"],
                "production",
                StringComparison.OrdinalIgnoreCase
            );

            _publicApiKey = _configuration["Payway:PublicApiKey"]
                ?? throw new InvalidOperationException("Payway:PublicApiKey no configurado");

            _privateApiKey = _configuration["Payway:PrivateApiKey"]
                ?? throw new InvalidOperationException("Payway:PrivateApiKey no configurado");

            _siteId = _configuration["Payway:SiteId"]
                ?? throw new InvalidOperationException("Payway:SiteId no configurado");

            _apiBaseUrl = _isProduction
                ? "https://live.decidir.com"
                : "https://developers.decidir.com";

            _logger.LogInformation(
                "Payway inicializado | Env: {Env} | SiteId: {SiteId}",
                _isProduction ? "PROD" : "SANDBOX",
                _siteId
            );
        }

        public async Task<CreateCheckoutResponse> CreateCheckoutAsync(
            CreateCheckoutRequest request,
            CancellationToken cancellationToken = default)
        {
            var transactionId = GenerateTransactionId(request.SaleId);
            var amountInCents = (int)(request.Amount * 100);

            var payload = new
            {
                site = new
                {
                    id = _siteId,
                    transaction_id = transactionId
                },
                customer = new
                {
                    id = $"cust_{request.SaleId}",
                    email = request.Customer.Email
                },
                payment = new
                {
                    amount = amountInCents,
                    currency = "ARS",
                    payment_type = "single"
                },
                success_url = request.ReturnUrl
                    ?? $"{_configuration["App:BaseUrl"]}/payments/success?saleId={request.SaleId}",
                cancel_url = request.CancelUrl
                    ?? $"{_configuration["App:BaseUrl"]}/payments/cancel?saleId={request.SaleId}"
            };

            var json = JsonSerializer.Serialize(payload);

            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_apiBaseUrl}/web/v1.2/forms/validate")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            httpRequest.Headers.Add("apikey", _privateApiKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Payway error {Status}: {Body}",
                    response.StatusCode,
                    content
                );

                throw new InvalidOperationException("Error creando checkout Payway");
            }

            var validate = JsonSerializer.Deserialize<FormValidateResponse>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? throw new InvalidOperationException("Respuesta inválida de Payway");

            var checkoutUrl =
                $"https://forms.decidir.com/web/forms/{validate.Hash}?apikey={_publicApiKey}";

            return new CreateCheckoutResponse
            {
                TransactionId = transactionId,
                CheckoutId = validate.Hash,
                CheckoutUrl = checkoutUrl
            };
        }

        // <-- IMPLEMENTACIÓN REQUERIDA POR LA INTERFAZ
        // Actualmente devuelve null (placeholder). Reemplazá por la llamada a Payway o DB cuando lo implementes.
        public Task<PaymentStatusResponse?> GetPaymentStatusAsync(
            string transactionId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetPaymentStatusAsync invoked for TransactionId: {Tx}", transactionId);

            // Placeholder: no hay implementación externa todavía.
            // Opciones futuras:
            // - Llamar a _httpClient.GetAsync($"/payments/{transactionId}") y mapear la respuesta
            // - Consultar PaymentTransactions en tu BD
            return Task.FromResult<PaymentStatusResponse?>(null);
        }

        private static string GenerateTransactionId(int saleId)
        {
            var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var rnd = RandomNumberGenerator.GetInt32(1000, 9999);
            return $"JOV_{ts}_{saleId}_{rnd}";
        }
    }
}
