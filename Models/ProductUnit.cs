// Models/ProductUnit.cs
namespace ForrajeriaJovitaAPI.Models
{
    public enum StockRoundingType
    {
        None = 0,
        RoundUp = 1,
        RoundDown = 2,
        RoundNearest = 3
    }

    public class ProductUnit
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string UnitLabel { get; set; } = string.Empty;
        public decimal ConversionToBase { get; set; }
        public bool AllowFractionalQuantity { get; set; }
        public decimal MinSellStep { get; set; }
        public string? Barcode { get; set; }
        public StockRoundingType StockRounding { get; set; }
        public int StockDecimals { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public Product Product { get; set; } = null!;
        public ICollection<ProductUnitPrice> ProductUnitPrices { get; set; } = new List<ProductUnitPrice>();
    }
}