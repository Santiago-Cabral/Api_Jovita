// Models/PaywayOptions.cs
using System.ComponentModel.DataAnnotations;

namespace ForrajeriaJovitaAPI.Models
{
    public class PaywayOptions
    {
        [Required]
        public string Environment { get; set; } = "sandbox"; // "sandbox" | "production"

        [Required]
        public string PublicApiKey { get; set; } = string.Empty;

        [Required]
        public string PrivateApiKey { get; set; } = string.Empty;

        // SiteId viene de User Secrets (es string porque Payway lo maneja así)
        [Required]
        public string SiteId { get; set; } = string.Empty;

        public string? SandboxApiBaseUrl { get; set; } = "https://developers.decidir.com";
        public string? LiveApiBaseUrl { get; set; } = "https://live.decidir.com";

        public string? WebhookSecret { get; set; }
    }
}
