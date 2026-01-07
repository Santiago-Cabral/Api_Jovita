namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    public class PaywayCheckoutResponse
    {
        public string CheckoutUrl { get; set; } = string.Empty;
        public string CheckoutId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
    }
}