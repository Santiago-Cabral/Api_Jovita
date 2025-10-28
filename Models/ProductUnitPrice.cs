// Models/ProductUnitPrice.cs
namespace ForrajeriaJovitaAPI.Models
{
    public enum PriceTier
    {
        Retail = 1,
        Wholesale = 2,
        Special = 3
    }

    public class ProductUnitPrice
    {
        public int Id { get; set; }
        public int ProductUnitId { get; set; }
        public PriceTier Tier { get; set; }
        public decimal Price { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public ProductUnit ProductUnit { get; set; } = null!;
    }
}