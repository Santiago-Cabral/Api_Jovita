// Models/CashMovementType.cs
namespace ForrajeriaJovitaAPI.Models
{
	/// <summary>
	/// Tipos de movimiento de caja.
	/// Ajustá los valores numéricos si tu BD ya usa códigos específicos.
	/// </summary>
	public enum CashMovementType
	{
		Undefined = 0,
		Sale = 1,             // movimiento por venta
		SaleReturn = 2,       // devolución
		StockAdjustment = 3,  // ajuste de stock
		Income = 4,           // ingreso varios
		Expense = 5,          // egreso varios
		Transfer = 6,         // transferencias entre cajas
		Other = 7             // otros
	}
}
