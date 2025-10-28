
// Models/CashSession.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class CashSession
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public int? OpenedByUserId { get; set; }
        public DateTime OpenedAt { get; set; }
        public decimal OpeningAmount { get; set; }
        public bool IsClosed { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int? ClosedByUserId { get; set; }
        public decimal? ClosingAmount { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public Branch Branch { get; set; } = null!;
        public User? OpenedByUser { get; set; }
        public User? ClosedByUser { get; set; }
        public ICollection<CashMovement> CashMovements { get; set; } = new List<CashMovement>();
    }
}
