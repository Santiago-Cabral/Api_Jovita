// Models/PaymentMethod.cs
namespace ForrajeriaJovitaAPI.Models
{
	/// <summary>
	/// Tipos de métodos de pago disponibles
	/// </summary>
	public enum PaymentMethod
	{
		/// <summary>
		/// Pago en efectivo
		/// </summary>
		Cash = 1,

		/// <summary>
		/// Pago con tarjeta (débito/crédito en POS)
		/// </summary>
		Card = 2,

		/// <summary>
		/// Transferencia bancaria
		/// </summary>
		Transfer = 3,

		/// <summary>
		/// Cuenta corriente / Crédito de la casa
		/// </summary>
		Credit = 4
	}
}