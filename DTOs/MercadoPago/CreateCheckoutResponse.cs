namespace ForrajeriaJovitaAPI.DTOs.MercadoPago
{
    public class CreateCheckoutResponse
    {
        public string CheckoutId { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
    }
}