// =====================================================
// DTOs/Payway/PaywayCheckoutRequest.cs
// =====================================================
namespace ForrajeriaJovitaAPI.DTOs.Payway
{
	/// <summary>
	/// Request para crear un checkout en Payway
	/// </summary>
	public class PaywayCheckoutRequest
	{
		/// <summary>
		/// ID de la venta en tu sistema
		/// </summary>
		public int SaleId { get; set; }

		/// <summary>
		/// Monto total en pesos (será convertido a centavos automáticamente)
		/// </summary>
		public decimal Amount { get; set; }

		/// <summary>
		/// Descripción del pago
		/// </summary>
		public string? Description { get; set; }

		/// <summary>
		/// Información del cliente
		/// </summary>
		public CustomerInfo? Customer { get; set; }

		/// <summary>
		/// URL de retorno exitoso (ej: https://tusite.com/pago-exitoso)
		/// </summary>
		public string? ReturnUrl { get; set; }

		/// <summary>
		/// URL de cancelación (ej: https://tusite.com/pago-cancelado)
		/// </summary>
		public string? CancelUrl { get; set; }
	}

	
}
