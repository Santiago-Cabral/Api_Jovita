// Models/ProductSeason.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class ProductSeason
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int SeasonId { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public Product Product { get; set; } = null!;
        public Season Season { get; set; } = null!;
    }
}