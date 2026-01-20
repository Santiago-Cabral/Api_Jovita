using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ForrajeriaJovitaAPI.DTOs.Payway;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IPaywayService
    {
        Task<CreateCheckoutResponse> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Servicio para integración con Decidir (Payway)
    /// Documentación: https://developers.decidir.com/
    /// </summary>
    public class PaywayService : IPaywayService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaywayService> _logger;

        private readonly string _apiBaseUrl;
        private readonly string _publicApiKey;
        private readonly string _privateApiKey;
        private readonly bool _isProduction;

        public PaywayService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<PaywayService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            // Configuración del ambiente
            _isProduction = _configuration["Payway:Environment"]?.ToLower() == "production";
            _publicApiKey = _configuration["Payway:PublicApiKey"]
                ?? throw new InvalidOperationException("Payway:PublicApiKey no configurado");
            _privateApiKey = _configuration["Payway:PrivateApiKey"]
                ?? throw new InvalidOperationException("Payway:PrivateApiKey no configurado");

            // URLs correctas según documentación de Decidir
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

                // 1. Generar ID de transacción único
                var transactionId = GenerateTransactionId(request.SaleId);
                _logger.LogDebug("🔑 [PAYWAY] TransactionId generado: {TransactionId}", transactionId);

                // 2. Preparar payload según documentación de Decidir
                var amountInCents = (int)(request.Amount * 100);

                var payload = new
                {
                    site_transaction_id = transactionId,
                    token = "cybersource", // Token para flujo redirect
                    customer = new
                    {
                        id = request.Customer?.Email?.Replace("@", "_").Replace(".", "_")
                            ?? $"customer_{request.SaleId}",
                        email = request.Customer?.Email ?? $"temp{request.SaleId}@example.com"
                    },
                    payment_method_id = 1, // 1 = Tarjeta de crédito
                    bin = "450799", // BIN requerido (se sobrescribe en el formulario)
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

                _logger.LogDebug("📦 [PAYWAY] Payload preparado: {Payload}", jsonPayload);

                // 3. Crear request HTTP
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_apiBaseUrl}/payments")
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                // Headers según documentación de Decidir
                httpRequest.Headers.Add("apikey", _publicApiKey);
                httpRequest.Headers.Add("Cache-Control", "no-cache");

                _logger.LogInformation("🌐 [PAYWAY] Enviando request a: {Url}", $"{_apiBaseUrl}/payments");

                // 4. Enviar request a Decidir
                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogInformation("📊 [PAYWAY] Response - Status: {StatusCode}, Length: {Length}",
                    response.StatusCode, responseContent?.Length ?? 0);

                // 5. Manejar errores
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ [PAYWAY] Error HTTP {StatusCode}: {Content}",
                        response.StatusCode, responseContent);

                    // Intentar parsear el error
                    string errorMessage = "Error desconocido de Payway";
                    try
                    {
                        var errorObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                        if (errorObj.TryGetProperty("error_type", out var errorType))
                        {
                            errorMessage = errorType.GetString() ?? errorMessage;
                        }
                        if (errorObj.TryGetProperty("message", out var message))
                        {
                            errorMessage += $": {message.GetString()}";
                        }
                    }
                    catch
                    {
                        errorMessage = responseContent;
                    }

                    throw new HttpRequestException($"Payway error {response.StatusCode}: {errorMessage}");
                }

                // 6. Parsear respuesta exitosa
                _logger.LogDebug("📄 [PAYWAY] Response content: {Content}", responseContent);

                var paywayResponse = JsonSerializer.Deserialize<PaywayPaymentResponse>(
                    responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (paywayResponse?.Id == null)
                {
                    _logger.LogError("❌ [PAYWAY] Respuesta inválida o sin ID");
                    throw new InvalidOperationException("Payway no devolvió un ID de pago válido");
                }

                // 7. Construir URL del checkout
                // Para el flujo de redirect, usamos el endpoint de forms con el payment_id
                var checkoutUrl = _isProduction
                    ? $"https://live.decidir.com/web/forms?payment_id={paywayResponse.Id}&apikey={_publicApiKey}"
                    : $"https://developers.decidir.com/web/forms?payment_id={paywayResponse.Id}&apikey={_publicApiKey}";

                _logger.LogInformation("✅ [PAYWAY] Pago creado exitosamente");
                _logger.LogInformation("   PaymentId: {PaymentId}", paywayResponse.Id);
                _logger.LogInformation("   TransactionId: {TransactionId}", transactionId);
                _logger.LogInformation("   CheckoutUrl: {Url}", checkoutUrl);

                return new CreateCheckoutResponse
                {
                    CheckoutUrl = checkoutUrl,
                    CheckoutId = paywayResponse.Id.ToString(),
                    TransactionId = transactionId
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "❌ [PAYWAY] Error de comunicación HTTP");
                throw new InvalidOperationException(
                    "No se pudo comunicar con el procesador de pagos. Intente nuevamente.", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "❌ [PAYWAY] Error al parsear respuesta JSON");
                throw new InvalidOperationException(
                    "El procesador de pagos devolvió una respuesta inválida.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [PAYWAY] Error inesperado");
                throw new InvalidOperationException(
                    "Error inesperado al procesar el pago. Por favor contacte a soporte.", ex);
            }
        }

        /// <summary>
        /// Genera un ID único de transacción con formato: JOV_TIMESTAMP_SALEID
        /// </summary>
        private string GenerateTransactionId(int saleId)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"JOV_{timestamp}_{saleId}_{random}";
        }
    }

    // ===================== DTOs =====================

 

    public class CustomerData
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    public class CreateCheckoutResponse
    {
        public string CheckoutUrl { get; set; } = string.Empty;
        public string CheckoutId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Respuesta de la API de Decidir al crear un pago
    /// </summary>
    public class PaywayPaymentResponse
    {
        public int? Id { get; set; }
        public string? Status { get; set; }
        public string? StatusDetails { get; set; }
        public int? Amount { get; set; }
        public string? Currency { get; set; }
        public string? Site_Transaction_Id { get; set; }
        public string? Token { get; set; }
        public DateTime? Date { get; set; }
        public CustomerInfo? Customer { get; set; }
        public string? Error_Type { get; set; }
        public ValidationErrors? Validation_Errors { get; set; }
    }

   

    public class ValidationErrors
    {
        public string[]? Code { get; set; }
        public string[]? Reason { get; set; }
    }
}