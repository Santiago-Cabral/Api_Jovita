using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForrajeriaJovitaAPI.DTOs.Payway;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services.Interfaces;

public class PaywayService : IPaywayService
{
    private readonly HttpClient _http;
    private readonly PaywayOptions _cfg;

    public PaywayService(HttpClient http, PaywayOptions cfg)
    {
        _http = http;
        _cfg = cfg;
    }

    public async Task<CreateCheckoutResponse> CreateCheckoutAsync(
        CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        // 1️⃣ TransactionId único (Payway lo necesita)
        var transactionId = $"SALE_{request.SaleId}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        // 2️⃣ Payload EXACTO que Payway espera
        var payload = new
        {
            site = new
            {
                id = _cfg.SiteId,
                transaction_id = transactionId
            },
            customer = new
            {
                email = request.Customer.Email
            },
            payment = new
            {
                amount = (int)(request.Amount * 100),
                currency = "ARS",
                payment_type = "single"
            },
            success_url = request.ReturnUrl,
            cancel_url = request.CancelUrl
        };

        var json = JsonSerializer.Serialize(payload);

        // 3️⃣ Firma HMAC SHA256
        var signature = Convert.ToBase64String(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(_cfg.PrivateKey),
                Encoding.UTF8.GetBytes(json)
            )
        );

        // 4️⃣ Request Payway
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/web/v1.2/forms/validate"
        );

        httpRequest.Headers.Add("apikey", _cfg.PublicKey);
        httpRequest.Headers.Add("x-signature", signature);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(httpRequest, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Payway error {response.StatusCode}: {content}"
            );
        }

        // 5️⃣ Respuesta Payway
        var validate = JsonSerializer.Deserialize<FormValidateResponse>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new Exception("Respuesta Payway inválida");

        // 6️⃣ URL FINAL DE REDIRECCIÓN
        var checkoutUrl =
            $"https://forms.decidir.com/web/forms/{validate.Hash}?apikey={_cfg.PublicKey}";

        return new CreateCheckoutResponse
        {
            CheckoutId = validate.Hash,
            TransactionId = transactionId,
            CheckoutUrl = checkoutUrl
        };
    }
}


