using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForrajeriaJovitaAPI.Models
{
    public class SaleItem
    {
        public int Id { get; set; }

        // FK a Sale
        public int SaleId { get; set; }
        public Sale? Sale { get; set; }

        // Producto
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        // Cantidad / precio / descuento
        // DB: decimal(18,2) -> C#: decimal
        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        // --- Nuevos campos que tu código espera ---
        // FK opcional a unidad de producto (si usás unidades: unit, pack, etc.)
        public int? ProductUnitId { get; set; }
        public ProductUnit? ProductUnit { get; set; }

        // Nombre de sucursal/branch (si tu CheckoutService arma un campo para mostrar)
        public string? BranchName { get; set; }

        // Conversiones/stock (DB decimal(18,2))
        [Column(TypeName = "decimal(18,2)")]
        public decimal ConversionToBase { get; set; } = 1m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeductedBaseQuantity { get; set; } = 0m;

        public DateTime CreationDate { get; set; } = DateTime.Now;
    }
}
