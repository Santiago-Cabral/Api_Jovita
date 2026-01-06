// =====================================================
// DTOs/Payway/PaywayPaymentStatus.cs
// =====================================================
namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    /// <summary>
    /// Estado de un pago consultado
    /// </summary>
    public class PaywayPaymentStatus
    {
        /// <summary>
        /// Estado: approved, rejected, pending
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Detalle del estado
        /// </summary>
        public string? StatusDetail { get; set; }

        /// <summary>
        /// Monto
        /// </summary>
        public decimal? Amount { get; set; }

        /// <summary>
        /// ID de transacción
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// ID del pago en Payway
        /// </summary>
        public string? PaymentId { get; set; }
    }
}