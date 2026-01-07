namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    public class PaywayCheckoutRequest
    {
        public int SaleId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public CustomerInfo? Customer { get; set; }
        public string? ReturnUrl { get; set; }
        public string? CancelUrl { get; set; }
    }
}