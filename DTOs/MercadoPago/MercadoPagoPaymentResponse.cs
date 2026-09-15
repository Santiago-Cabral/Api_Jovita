namespace ForrajeriaJovitaAPI.DTOs.MercadoPago
{
    public class MercadoPagoPaymentResponse
    {
        public long Id { get; set; }
        public string? Status { get; set; }
        public string? StatusDetail { get; set; }
        public decimal TransactionAmount { get; set; }
        public string? ExternalReference { get; set; }
    }

    // Usado solo para leer el webhook entrante (type=payment, data.id=...)
    public class MercadoPagoWebhookNotification
    {
        public string? Type { get; set; }
        public string? Action { get; set; }
        public MercadoPagoWebhookData? Data { get; set; }
    }

    public class MercadoPagoWebhookData
    {
        public string? Id { get; set; }
    }
}