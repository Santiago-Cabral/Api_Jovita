// Services/PaywayService.cs
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IPaywayService
    {
        /// <summary>
        /// Crea un checkout en Payway. Devuelve preferentemente la URL de checkout (checkout_url/payment_url/...).
        /// Si Payway no devuelve una URL, devuelve el body raw (JSON).
        /// </summary>
        Task<string> CreatePaymentAsync(decimal amount, string description, string? transactionId = null);

        /// <summary>
        /// Consulta estado de pago (intenta la ruta /v1/payments/{transactionId} por defecto).
        /// </summary>
        Task<PaywayPaymentStatus?> GetPaymentStatusAsync(string transactionId);
    }

    public class PaywayService : IPaywayService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<PaywayService> _logger;

        public PaywayService(IConfiguration config, IHttpClientFactory httpFactory, ILogger<PaywayService> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> CreatePaymentAsync(decimal amount, string description, string? transactionId = null)
        {
            var apiUrl = (_config["Payway:ApiUrl"] ?? "").TrimEnd('/');
            var checkoutPath = _config["Payway:CheckoutPath"] ?? "/v1/checkouts";
            var authType = (_config["Payway:AuthType"] ?? "ApiKey").ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(apiUrl))
                throw new InvalidOperationException("Payway:ApiUrl no configurado");

            // Credenciales
            var publicKey = _config["Payway:PublicKey"];
            var privateKey = _config["Payway:PrivateKey"];

            var bodyObj = new
            {
                site_transaction_id = transactionId ?? Guid.NewGuid().ToString(),
                token = publicKey,
                amount = amount,
                currency = "ARS",
                installments = 1,
                description = description
            };

            // Use named client "payway" if present (con BaseAddress configurado), else default client
            var client = CreateHttpClient();

            client.Timeout = TimeSpan.FromSeconds(30);

            // Autenticación
            if (authType == "basic")
            {
                if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
                    throw new InvalidOperationException("PublicKey/PrivateKey requeridos para AuthType=Basic");

                var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{privateKey}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
            }
            else if (authType == "oauth2")
            {
                // OAuth2 Client Credentials
                var token = await GetOAuthTokenAsync();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else // ApiKey por defecto
            {
                if (string.IsNullOrEmpty(publicKey))
                    throw new InvalidOperationException("PublicKey requerido para AuthType=ApiKey");

                // algunos providers aceptan "X-API-KEY" o "apikey" — usamos X-API-KEY
                client.DefaultRequestHeaders.Remove("X-API-KEY");
                client.DefaultRequestHeaders.Remove("apikey");
                client.DefaultRequestHeaders.Add("X-API-KEY", publicKey);
            }

            var jsonContent = JsonSerializer.Serialize(bodyObj);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var endpoint = $"{apiUrl}{checkoutPath}";

            _logger.LogInformation("Payway CreatePayment -> POST {Endpoint} payload size={PayloadSize}", endpoint, jsonContent.Length);

            using var resp = await client.PostAsync(endpoint, content);
            var raw = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Payway CreatePayment error: {Status} - {Body}", resp.StatusCode, raw);
                // Devolver contenido raw para diagnóstico o lanzar excepción según tu política
                throw new InvalidOperationException($"Payway error: {resp.StatusCode} - {TrimForLog(raw)}");
            }

            // Intentar extraer la URL de checkout de forma robusta
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                var url = TryGetCheckoutUrl(root);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    _logger.LogInformation("Payway checkout url encontrada: {Url}", url);
                    return url;
                }
            }
            catch (JsonException je)
            {
                _logger.LogWarning(je, "Respuesta de Payway no es JSON válido");
            }

            // Si no encontramos URL, retornar raw JSON (o string)
            _logger.LogWarning("No se encontró checkout URL en la respuesta de Payway. Devolviendo raw body.");
            return raw;
        }

        public async Task<PaywayPaymentStatus?> GetPaymentStatusAsync(string transactionId)
        {
            var apiUrl = (_config["Payway:ApiUrl"] ?? "").TrimEnd('/');
            var authType = (_config["Payway:AuthType"] ?? "ApiKey").ToLowerInvariant();
            var publicKey = _config["Payway:PublicKey"];

            if (string.IsNullOrWhiteSpace(apiUrl))
                throw new InvalidOperationException("Payway:ApiUrl no configurado");

            var client = CreateHttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            if (authType == "basic")
            {
                var privateKey = _config["Payway:PrivateKey"];
                if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
                    throw new InvalidOperationException("PublicKey/PrivateKey requeridos para AuthType=Basic");
                var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{privateKey}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
            }
            else if (authType == "oauth2")
            {
                var token = await GetOAuthTokenAsync();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else // ApiKey
            {
                if (string.IsNullOrEmpty(publicKey))
                    throw new InvalidOperationException("PublicKey requerido para AuthType=ApiKey");
                client.DefaultRequestHeaders.Remove("X-API-KEY");
                client.DefaultRequestHeaders.Add("X-API-KEY", publicKey);
            }

            var paymentPath = _config["Payway:PaymentStatusPath"] ?? "/v1/payments/{0}";
            var endpoint = string.Format($"{apiUrl}{paymentPath}", transactionId);

            try
            {
                using var resp = await client.GetAsync(endpoint);
                var raw = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Payway GetPaymentStatus non-success: {Status} - {Body}", resp.StatusCode, TrimForLog(raw));
                    return null;
                }

                var payment = JsonSerializer.Deserialize<PaywayPaymentStatus>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return payment;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error consultando estado de pago en Payway");
                return null;
            }
        }

        #region Helpers

        private HttpClient CreateHttpClient()
        {
            // Intenta obtener el cliente nombrado "payway" (registrado en Program.cs si deseás)
            try
            {
                return _httpFactory.CreateClient("payway");
            }
            catch
            {
                // fallback al cliente por defecto
                return _httpFactory.CreateClient();
            }
        }

        private async Task<string> GetOAuthTokenAsync()
        {
            // Espera: Payway:TokenUrl, Payway:ClientId, Payway:ClientSecret
            var tokenUrl = _config["Payway:TokenUrl"];
            var clientId = _config["Payway:ClientId"];
            var clientSecret = _config["Payway:ClientSecret"];

            if (string.IsNullOrEmpty(tokenUrl) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                throw new InvalidOperationException("Payway OAuth2 requiere TokenUrl, ClientId y ClientSecret en configuración.");

            var client = _httpFactory.CreateClient();
            var form = new[]
            {
                new KeyValuePair<string,string>("grant_type","client_credentials"),
                new KeyValuePair<string,string>("client_id", clientId),
                new KeyValuePair<string,string>("client_secret", clientSecret)
            };

            using var resp = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
            var raw = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Error obteniendo OAuth token de Payway: {Status} - {Body}", resp.StatusCode, TrimForLog(raw));
                throw new InvalidOperationException($"Error obteniendo token: {resp.StatusCode} - {TrimForLog(raw)}");
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("access_token", out var t))
                    return t.GetString()!;
            }
            catch (JsonException)
            {
                _logger.LogError("OAuth token response no es JSON válido: {Raw}", TrimForLog(raw));
            }

            throw new InvalidOperationException("No se encontró access_token en la respuesta de token.");
        }

        private static string? TryGetCheckoutUrl(JsonElement root)
        {
            // patrones comunes
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("checkout_url", out var v) && v.ValueKind == JsonValueKind.String) return v.GetString();
                if (root.TryGetProperty("payment_url", out v) && v.ValueKind == JsonValueKind.String) return v.GetString();
                if (root.TryGetProperty("redirect_url", out v) && v.ValueKind == JsonValueKind.String) return v.GetString();
                if (root.TryGetProperty("url", out v) && v.ValueKind == JsonValueKind.String) return v.GetString();

                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    if (data.TryGetProperty("checkout_url", out var d1) && d1.ValueKind == JsonValueKind.String) return d1.GetString();
                    if (data.TryGetProperty("url", out var d2) && d2.ValueKind == JsonValueKind.String) return d2.GetString();
                }

                if (root.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Object)
                {
                    if (links.TryGetProperty("checkout", out var l1) && l1.ValueKind == JsonValueKind.String) return l1.GetString();
                    if (links.TryGetProperty("self", out var l2) && l2.ValueKind == JsonValueKind.String) return l2.GetString();
                }

                // buscar en cualquier propiedad que sea objeto con 'url' o 'checkout_url'
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (prop.Value.TryGetProperty("url", out var px) && px.ValueKind == JsonValueKind.String) return px.GetString();
                        if (prop.Value.TryGetProperty("checkout_url", out var px2) && px2.ValueKind == JsonValueKind.String) return px2.GetString();
                    }
                }
            }

            return null;
        }

        private static string TrimForLog(string? s, int max = 1000)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + "...(truncated)";
        }

        #endregion
    }
}