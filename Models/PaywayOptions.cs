using System.ComponentModel.DataAnnotations;

namespace ForrajeriaJovitaAPI
{
    /// <summary>
    /// Configuración para Payway. Se lee desde appsettings o User Secrets.
    /// </summary>
    public class PaywayOptions
    {
        /// <summary> "sandbox" o "production" </summary>
        public string? Environment { get; set; }

        /// <summary> SiteId que te provee Payway (leer desde User Secrets) </summary>
        public string? SiteId { get; set; }

        public string? PublicApiKey { get; set; }
        public string? PrivateApiKey { get; set; }

        /// <summary> Opcional: url base si necesitas sobrescribir defaults </summary>
        public string? LiveApiBaseUrl { get; set; }
        public string? SandboxApiBaseUrl { get; set; }

        /// <summary> Secret para validación de webhooks (opcional pero recomendado) </summary>
        public string? WebhookSecret { get; set; }
    }
}
