namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    public class PaywayPaymentStatus
    {
        public string? Status { get; set; }
        public string? StatusDetail { get; set; }
        public decimal? Amount { get; set; }
        public string? TransactionId { get; set; }
        public string? PaymentId { get; set; }
    }
}