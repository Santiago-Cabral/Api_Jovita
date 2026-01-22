namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    public class PaymentStatusResponse
    {
        public string TransactionId { get; set; } = string.Empty;
        public int SaleId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? StatusDetail { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "ARS";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
