using System;

namespace ForrajeriaJovitaAPI.Models
{
    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public Sale? Sale { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }

        public decimal ConversionToBase { get; set; }
        public decimal DeductedBaseQuantity { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.Now;
    }
}
