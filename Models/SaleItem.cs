// Models/SaleItem.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public string? BranchName { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public decimal ConversionToBase { get; set; }
        public decimal DeductedBaseQuantity { get; set; }
        public int? ProductUnitId { get; set; }

        // Navegación
        public Sale Sale { get; set; } = null!;
        public Product Product { get; set; } = null!;
        public ProductUnit? ProductUnit { get; set; }
    }
}
