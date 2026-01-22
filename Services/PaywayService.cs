using ForrajeriaJovitaAPI.DTOs.Payway;
using ForrajeriaJovitaAPI.Data;
using Microsoft.EntityFrameworkCore;

public class PaywayService : IPaywayService
{
    private readonly HttpClient _http;
    private readonly PaywayOptions _cfg;
    private readonly ForrajeriaContext _db;

    public PaywayService(HttpClient http, PaywayOptions cfg, ForrajeriaContext db)
    {
        _http = http;
        _cfg = cfg;
        _db = db;
    }

    // =============================
    // CREATE CHECKOUT
    // =============================
    public async Task<CreateCheckoutResponse> CreateCheckoutAsync(
        CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var transactionId = $"SALE_{request.SaleId}_{DateTime.UtcNow:yyyyMMddHHmmss}";

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

        var signature = Convert.ToBase64String(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(_cfg.PrivateKey),
                Encoding.UTF8.GetBytes(json)
            )
        );

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
            throw new Exception($"Payway error {response.StatusCode}: {content}");

        var validate = JsonSerializer.Deserialize<FormValidateResponse>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new Exception("Respuesta Payway inválida");

        return new CreateCheckoutResponse
        {
            CheckoutId = validate.Hash,
            TransactionId = transactionId,
            CheckoutUrl = $"https://forms.decidir.com/web/forms/{validate.Hash}?apikey={_cfg.PublicKey}"
        };
    }

    // =============================
    // GET PAYMENT STATUS (DESDE BD)
    // =============================
    public async Task<PaymentStatusResponse?> GetPaymentStatusAsync(
        string transactionId,
        CancellationToken cancellationToken)
    {
        var tx = await _db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId, cancellationToken);

        if (tx == null) return null;

        return new PaymentStatusResponse
        {
            TransactionId = tx.TransactionId,
            SaleId = tx.SaleId,
            Status = tx.Status,
            StatusDetail = tx.StatusDetail,
            Amount = tx.Amount,
            Currency = tx.Currency,
            CreatedAt = tx.CreatedAt,
            UpdatedAt = tx.UpdatedAt,
            CompletedAt = tx.CompletedAt
        };
    }
}



