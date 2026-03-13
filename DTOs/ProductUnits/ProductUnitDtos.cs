namespace ForrajeriaJovitaAPI.DTOs.ProductUnits
{
    public class ProductUnitPriceDto
    {
        public int Id { get; set; }
        public string Tier { get; set; } = string.Empty;
        public int TierValue { get; set; }
        public decimal Price { get; set; }
    }

    public class ProductUnitDto
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string UnitLabel { get; set; } = string.Empty;
        public decimal ConversionToBase { get; set; }
        public bool AllowFractionalQuantity { get; set; }
        public decimal MinSellStep { get; set; }
        public string? Barcode { get; set; }
        public int StockDecimals { get; set; }
        public decimal PercentageIncrease { get; set; }
        public decimal? RetailPrice { get; set; }
        public List<ProductUnitPriceDto> Prices { get; set; } = new();
    }

    public class ProductUnitsResponseDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public List<ProductUnitDto> Units { get; set; } = new();
    }

    public class ProductWithBaseUnitDto
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Image { get; set; }
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public bool IsFeatured { get; set; }
        public string BaseUnitLabel { get; set; } = string.Empty;
        public string BaseUnitDisplayName { get; set; } = string.Empty;
        public decimal? BaseRetailPrice { get; set; }
        public int UnitCount { get; set; }
    }

    /// <summary>DTO para crear o editar una unidad de venta.</summary>
    public class CreateProductUnitDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string UnitLabel { get; set; } = string.Empty;
        public decimal ConversionToBase { get; set; } = 1;
        public bool AllowFractionalQuantity { get; set; }
        public decimal MinSellStep { get; set; } = 1;
        public string? Barcode { get; set; }
        public int StockDecimals { get; set; } = 0;

        /// <summary>Precio Retail. Si se envía, se crea/actualiza el precio vigente.</summary>
        public decimal? RetailPrice { get; set; }
    }
}