using System;

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
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }

        // --- Nuevos campos que tu código espera ---
        // FK opcional a unidad de producto (si usás unidades: unit, pack, etc.)
        public int? ProductUnitId { get; set; }
        public ProductUnit? ProductUnit { get; set; }

        // Nombre de sucursal/branch (si tu CheckoutService arma un campo para mostrar)
        // Si prefieres evitar persistir este campo, hacelo nullable; mantenerlo facilita compilación.
        public string? BranchName { get; set; }

        // Conversiones/stock
        public decimal ConversionToBase { get; set; } = 1m;
        public decimal DeductedBaseQuantity { get; set; } = 0m;

        public DateTime CreationDate { get; set; } = DateTime.Now;
    }
}
