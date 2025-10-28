// Models/Product.cs
namespace ForrajeriaJovitaAPI.Models
{
    public enum BaseUnit
    {
        Kilogram = 1,
        Unit = 2,
        Liter = 3
    }

    public class Product
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActived { get; set; }
        public DateTime? UpdateDate { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public BaseUnit BaseUnit { get; set; }

        // Navegación
        public ICollection<ProductSeason> ProductsSeasons { get; set; } = new List<ProductSeason>();
        public ICollection<ProductStock> ProductsStocks { get; set; } = new List<ProductStock>();
        public ICollection<ProductUnit> ProductUnits { get; set; } = new List<ProductUnit>();
        public ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
        public ICollection<SaleItem> SalesItems { get; set; } = new List<SaleItem>();
    }
}