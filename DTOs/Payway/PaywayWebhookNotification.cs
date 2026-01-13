// DTOs/Payway/PaywayWebhookNotification.cs
namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    /// <summary>
    /// Notificación webhook recibida de Payway
    /// </summary>
    public class PaywayWebhookNotification
    {
        public string? SiteTransactionId { get; set; }
        public string? Status { get; set; }
        public string? StatusDetail { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public string? PaymentId { get; set; }
        public string? PaymentMethod { get; set; }
    }
}
