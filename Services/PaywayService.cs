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

        public PaywayService(HttpClient httpClient, PaywayOptions options, ILogger<PaywayService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("🔧 [PAYWAY] Servicio inicializado. BaseAddress={Base}", _httpClient.BaseAddress);
        }

        public async Task<CreateCheckoutResponse> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("💳 [PAYWAY] Creando checkout - Sale:{Sale} Amount:{Amount}", request.SaleId, request.Amount);

                var transactionId = $"JOV_{DateTime.UtcNow:yyyyMMddHHmmss}_{request.SaleId}_{new Random().Next(1000, 9999)}";
                var amountInCents = (int)(request.Amount * 100);

                var payload = new
                {
                    site_transaction_id = transactionId,
                    token = "cybersource",
                    customer = new
                    {
                        id = SanitizeEmail(request.Customer?.Email ?? $"cust{request.SaleId}"),
                        email = request.Customer?.Email ?? $"temp{request.SaleId}@example.com"
                    },
                    payment_method_id = 1,
                    bin = "450799",
                    amount = amountInCents,
                    currency = "ARS",
                    installments = 1,
                    description = request.Description ?? $"Pedido #{request.SaleId}",
                    payment_type = "single",
                    sub_payments = Array.Empty<object>()
                };

                var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Construimos HttpRequestMessage y añadimos cabecera apikey (por petición)
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "payments")
                {
                    Content = content
                };

                if (string.IsNullOrWhiteSpace(_options.PublicKey))
                {
                    _logger.LogError("❌ [PAYWAY] PublicKey no configurada en PaywayOptions.");
                    throw new InvalidOperationException("Configuración Payway inválida");
                }

                httpRequest.Headers.Remove("apikey");
                httpRequest.Headers.Add("apikey", _options.PublicKey);

                _logger.LogDebug("📦 [PAYWAY] Payload enviado: {Payload}", jsonPayload);
                _logger.LogInformation("🌐 [PAYWAY] POST {Url} (relative to BaseAddress)", "payments");

                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

                // Loguear siempre el body para depuración (no claves secretas)
                _logger.LogDebug("📊 [PAYWAY] Status: {Status}. Body: {Body}", (int)response.StatusCode, responseText);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ [PAYWAY] Error creando checkout. Status: {Status}. Body: {Body}", (int)response.StatusCode, responseText);
                    // devolvemos error genérico al frontend (el detalle queda en logs)
                    throw new InvalidOperationException("Error al crear el checkout de pago");
                }

                var paywayResp = JsonSerializer.Deserialize<PaywayPaymentResponse>(responseText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (paywayResp == null || paywayResp.Id == null)
                {
                    _logger.LogError("❌ [PAYWAY] Respuesta inválida de Payway: {Body}", responseText);
                    throw new InvalidOperationException("Respuesta inválida de Payway");
                }

                var baseUrl = _options.ApiUrl?.Replace("/api/v2", "").TrimEnd('/') ?? _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "";
                var checkoutUrl = $"{baseUrl}/web/forms?payment_id={paywayResp.Id}&apikey={_options.PublicKey}";

                _logger.LogInformation("✅ [PAYWAY] Checkout OK - ID: {Id}, URL: {Url}", paywayResp.Id, checkoutUrl);

                return new CreateCheckoutResponse
                {
                    CheckoutId = paywayResp.Id.ToString(),
                    CheckoutUrl = checkoutUrl,
                    TransactionId = transactionId
                };
            }
            catch (HttpRequestException hx)
            {
                _logger.LogError(hx, "❌ [PAYWAY] HttpRequestException creando checkout");
                throw new InvalidOperationException("Error interno del servidor: problema de conexión con Payway");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [PAYWAY] Excepción creando checkout");
                throw;
            }
        }

        public async Task<PaymentStatusResponse?> GetPaymentStatusAsync(string paymentId, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"payments/{paymentId}");
                request.Headers.Remove("apikey");
                request.Headers.Add("apikey", _options.PublicKey);

                var resp = await _httpClient.SendAsync(request, cancellationToken);
                if (!resp.IsSuccessStatusCode) return null;
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<PaymentStatusResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo estado de pago");
                return null;
            }
        }

        private string SanitizeEmail(string email)
        {
            return (email ?? string.Empty)
                .Replace("@", "_at_")
                .Replace(".", "_")
                .Replace("+", "_")
                .Replace(" ", "_");
        }
    }
}

