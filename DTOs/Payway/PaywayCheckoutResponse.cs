// =====================================================
// DTOs/Payway/PaywayCheckoutResponse.cs
// =====================================================
namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    /// <summary>
    /// Respuesta del backend al crear un checkout
    /// </summary>
    public class PaywayCheckoutResponse
    {
        /// <summary>
        /// URL del formulario de Payway donde redirigir al usuario
        /// Ejemplo: https://api.decidir.com/web/form?hash=abc123...
        /// </summary>
        public string CheckoutUrl { get; set; } = string.Empty;

        /// <summary>
        /// ID del checkout en Payway (el hash)
        /// </summary>
        public string CheckoutId { get; set; } = string.Empty;

        /// <summary>
        /// ID de transacción generado por tu sistema
        /// Ejemplo: JOV_20260106123045_123
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;
    }
}