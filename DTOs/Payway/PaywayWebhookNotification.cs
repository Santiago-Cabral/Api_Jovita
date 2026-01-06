// =====================================================
// DTOs/Payway/PaywayWebhookNotification.cs
// =====================================================
namespace ForrajeriaJovitaAPI.DTOs.Payway
{
	/// <summary>
	/// Notificación que Payway envía al webhook cuando cambia el estado del pago
	/// </summary>
	public class PaywayWebhookNotification
	{
		/// <summary>
		/// Tu TransactionId original (site_transaction_id)
		/// </summary>
		public string? SiteTransactionId { get; set; }

		/// <summary>
		/// Estado del pago: approved, rejected, pending
		/// </summary>
		public string? Status { get; set; }

		/// <summary>
		/// Detalle del estado (motivo de rechazo, etc.)
		/// </summary>
		public string? StatusDetail { get; set; }

		/// <summary>
		/// Monto del pago
		/// </summary>
		public decimal? Amount { get; set; }

		/// <summary>
		/// Moneda (ARS)
		/// </summary>
		public string? Currency { get; set; }

		/// <summary>
		/// ID del pago en Payway
		/// </summary>
		public string? PaymentId { get; set; }

		/// <summary>
		/// Método de pago usado
		/// </summary>
		public string? PaymentMethod { get; set; }
	}
}