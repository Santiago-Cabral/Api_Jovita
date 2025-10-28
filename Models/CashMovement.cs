// Models/CashMovement.cs
namespace ForrajeriaJovitaAPI.Models
{
    public enum CashMovementType
    {
        Income = 1,
        Expense = 2,
        Sale = 3
    }

    public class CashMovement
    {
        public int Id { get; set; }
        public int CashSessionId { get; set; }
        public CashMovementType Type { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public CashSession CashSession { get; set; } = null!;
        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}
