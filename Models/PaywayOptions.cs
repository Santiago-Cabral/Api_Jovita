namespace ForrajeriaJovitaAPI
{
    public class PaywayOptions
    {
        public string Environment { get; set; } = "sandbox";
        public string PublicApiKey { get; set; } = string.Empty;
        public string PrivateApiKey { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public string SandboxApiBaseUrl { get; set; } = "https://developers.decidir.com/api/v2";
        public string LiveApiBaseUrl { get; set; } = "https://live.decidir.com/api/v2";
        public string FormsBaseUrl { get; set; } = "https://forms.decidir.com/web/forms";
    }
}
