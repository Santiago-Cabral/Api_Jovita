using System.Text.Json.Serialization;

namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    public class CreateCheckoutResponse
    {
        public string TransactionId { get; set; } = string.Empty;
        public string CheckoutId { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
    }
}
