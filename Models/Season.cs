// Models/Season.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class Season
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActived { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public ICollection<ProductSeason> ProductsSeasons { get; set; } = new List<ProductSeason>();
    }
}
