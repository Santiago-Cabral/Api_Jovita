// Models/Promotion.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class Promotion
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public int ProductId { get; set; }
        public DateTime ExpirationDate { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public Product Product { get; set; } = null!;
    }
}