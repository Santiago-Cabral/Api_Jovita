using System.Text.Json.Serialization;

namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    public class PaywayPaymentResponse
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("amount")]
        public long? Amount { get; set; }

        // agrega más campos si la API devuelve otros
    }
}
