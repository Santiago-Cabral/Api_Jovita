// File: DTOs/Payway/FormValidateResponse.cs
using System.Text.Json.Serialization;

namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    public class FormValidateResponse
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;
    }
}
