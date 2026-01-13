// DTOs/Payway/PaymentStatusResponse.cs
namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    /// <summary>
    /// Respuesta al consultar el estado de un pago
    /// </summary>
    public class PaymentStatusResponse
    {
        public string Status { get; set; } = string.Empty;
        public string? StatusDetail { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "ARS";
        public string? TransactionId { get; set; }
        public string? PaymentId { get; set; }
    }
}