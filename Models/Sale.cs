// Models/Sale.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class Sale
    {
        public int Id { get; set; }
        public int CashMovementId { get; set; }
        public DateTime SoldAt { get; set; }
        public int SellerUserId { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal Total { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public CashMovement CashMovement { get; set; } = null!;
        public User SellerUser { get; set; } = null!;
        public ICollection<SaleItem> SalesItems { get; set; } = new List<SaleItem>();
        public ICollection<SalePayment> SalesPayments { get; set; } = new List<SalePayment>();
    }
}