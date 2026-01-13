using System.Text.Json.Serialization;

namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    public class CheckoutResponse
    {
        [JsonPropertyName("checkout_url")]
        public string CheckoutUrl { get; set; } = string.Empty;

        [JsonPropertyName("checkout_id")]
        public string CheckoutId { get; set; } = string.Empty;

        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; } = string.Empty;
    }
}
