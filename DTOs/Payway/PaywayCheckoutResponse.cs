n// DTOs/Payway/CheckoutResponse.cs
namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    /// <summary>
    /// Respuesta al crear un checkout (para devolver al frontend)
    /// </summary>
    public class CheckoutResponse
    {
        public string CheckoutUrl { get; set; } = string.Empty;
        public string CheckoutId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
    }
}