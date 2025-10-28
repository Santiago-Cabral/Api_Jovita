// Models/Branch.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public bool IsActived { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public ICollection<CashSession> CashSessions { get; set; } = new List<CashSession>();
        public ICollection<ProductStock> ProductsStocks { get; set; } = new List<ProductStock>();
    }
}
