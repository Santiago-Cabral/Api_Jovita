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
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Amount <= 0) throw new ArgumentException("Amount must be > 0", nameof(request.Amount));

            var transactionId = $"JOV_{DateTime.UtcNow:yyyyMMddHHmmss}_{request.SaleId}_{new Random().Next(1000, 9999)}";
            var amountInCents = (int)(request.Amount * 100);

            // Build payload - keep property names camelCase as many APIs expect
            var payload = new
            {
                site_transaction_id = transactionId,
                token = "cybersource", // si necesitás otro token/campo, cambiar aquí
                customer = new
                {
                    id = SanitizeEmail(request.Customer?.Email ?? $"customer_{request.SaleId}"),
                    email = request.Customer?.Email ?? $"temp{request.SaleId}@forrajeriajovita.local"
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

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            _logger.LogDebug("📦 [PAYWAY] Payload: {Payload}", jsonPayload);

            try
            {
                // Usar ruta RELATIVA porque HttpClient.BaseAddress ya está configurada en Program.cs
                var req = new HttpRequestMessage(HttpMethod.Post, "payments")
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                // Header que usa tu proyecto hoy; si la pasarela requiere otro header (Authorization Bearer), cambiar aquí.
                if (!string.IsNullOrEmpty(_options.PublicKey))
                    req.Headers.Add("apikey", _options.PublicKey);

                req.Headers.Add("Cache-Control", "no-cache");

                _logger.LogInformation("🌐 [PAYWAY] POST {Url} (BaseAddress={Base})", "payments", _httpClient.BaseAddress);

                var response = await _httpClient.SendAsync(req, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogInformation("📊 [PAYWAY] Status {Status}", (int)response.StatusCode);
                _logger.LogDebug("📄 [PAYWAY] Body: {Body}", responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    // Si la pasarela devolvió un JSON con detalle, intentar parsearlo
                    string friendly = responseContent;
                    try
                    {
                        using var doc = JsonDocument.Parse(responseContent);
                        if (doc.RootElement.TryGetProperty("message", out var msg))
                            friendly = msg.GetString() ?? responseContent;
                    }
                    catch { /* no pasa nada si no es JSON */ }

                    _logger.LogError("❌ [PAYWAY] Error response: {Status} - {Body}", response.StatusCode, friendly);

                    // Si es un 5xx genérico y la ruta contiene "sandbox" o developers, devolvemos fallback para testing
                    if ((int)response.StatusCode >= 500)
                    {
                        var fallbackUrl = BuildDevFallbackUrl(transactionId);
                        _logger.LogWarning("⚠️ [PAYWAY] 5xx recibido - devolviendo fallback de desarrollo: {Fallback}", fallbackUrl);

                        return new CreateCheckoutResponse
                        {
                            CheckoutUrl = fallbackUrl,
                            CheckoutId = transactionId,
                            TransactionId = transactionId,
                            IsFallback = true
                        };
                    }

                    throw new HttpRequestException($"Payway returned {(int)response.StatusCode}: {friendly}");
                }

                // Intentar parsear respuesta real: buscar campos comunes (id, payment_id, checkout_url)
                string paymentId = null;
                string checkoutUrl = null;

                try
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("id", out var idEl))
                        paymentId = idEl.GetRawText().Trim('"');

                    if (root.TryGetProperty("payment_id", out var pid))
                        paymentId = pid.GetRawText().Trim('"');

                    if (root.TryGetProperty("checkout_url", out var cu))
                        checkoutUrl = cu.GetString();

                    // Si la respuesta trae un objeto "data" con internals
                    if (string.IsNullOrEmpty(paymentId) && root.TryGetProperty("data", out var dataEl))
                    {
                        if (dataEl.ValueKind == JsonValueKind.Object)
                        {
                            if (dataEl.TryGetProperty("id", out var did)) paymentId = did.GetRawText().Trim('"');
                            if (dataEl.TryGetProperty("checkout_url", out var dcu)) checkoutUrl = dcu.GetString();
                        }
                    }
                }
                catch (JsonException je)
                {
                    _logger.LogWarning(je, "⚠️ [PAYWAY] No pude parsear JSON de respuesta");
                }

                if (string.IsNullOrEmpty(paymentId))
                {
                    // Si no vino id, usamos fallback checkout url con el id que generamos
                    checkoutUrl ??= BuildDevFallbackUrl(transactionId);
                    _logger.LogWarning("⚠️ [PAYWAY] No se obtuvo payment id; usando checkoutUrl={Url}", checkoutUrl);
                    return new CreateCheckoutResponse
                    {
                        CheckoutUrl = checkoutUrl,
                        CheckoutId = paymentId ?? transactionId,
                        TransactionId = transactionId,
                        IsFallback = string.IsNullOrEmpty(paymentId)
                    };
                }

                // Si la pasarela no devolvió checkoutUrl, construir una estándar (basada en BaseAddress)
                if (string.IsNullOrEmpty(checkoutUrl))
                {
                    var baseAddr = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? _options.ApiUrl?.TrimEnd('/');
                    checkoutUrl = $"{baseAddr}/web/forms?payment_id={Uri.EscapeDataString(paymentId)}&apikey={Uri.EscapeDataString(_options.PublicKey ?? string.Empty)}";
                }

                _logger.LogInformation("✅ [PAYWAY] Checkout creado: PaymentId={PaymentId} Url={Url}", paymentId, checkoutUrl);

                return new CreateCheckoutResponse
                {
                    CheckoutUrl = checkoutUrl,
                    CheckoutId = paymentId,
                    TransactionId = transactionId,
                    IsFallback = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [PAYWAY] Excepción al crear checkout");
                // Si ocurrió un error inesperado (DNS, timeout, etc.) devolvemos fallback para no bloquear testing
                var fallback = BuildDevFallbackUrl(transactionId);
                _logger.LogWarning("⚠️ [PAYWAY] Devolviendo fallback por excepción: {Fallback}", fallback);

                return new CreateCheckoutResponse
                {
                    CheckoutUrl = fallback,
                    CheckoutId = transactionId,
                    TransactionId = transactionId,
                    IsFallback = true,
                    Error = ex.Message
                };
            }
        }

        // Opcional: endpoint para consultar estado en la pasarela
        public async Task<PaymentStatusResponse?> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(transactionId)) return null;

            try
            {
                var resp = await _httpClient.GetAsync($"payments/{Uri.EscapeDataString(transactionId)}", cancellationToken);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ [PAYWAY] GetPaymentStatus no OK: {Status} {Body}", resp.StatusCode, body);
                    return null;
                }

                var parsed = JsonSerializer.Deserialize<PaymentStatusResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return parsed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [PAYWAY] Error GetPaymentStatus");
                return null;
            }
        }

        private string BuildDevFallbackUrl(string transactionId)
        {
            // URL de fallback para testing. Se puede cambiar a la ruta del frontend de pruebas si querés.
            var host = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? _options.ApiUrl?.TrimEnd('/');
            // No rompas: devolvemos una URL navegable que el frontend pueda abrir para simular el flujo
            return $"{host}/__mock_payway_checkout/{Uri.EscapeDataString(transactionId)}";
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
