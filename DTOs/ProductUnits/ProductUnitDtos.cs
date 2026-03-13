namespace ForrajeriaJovitaAPI.DTOs.ProductUnits
{
    public class ProductUnitPriceDto
    {
        public int Id { get; set; }

        /// <summary>"Retail", "Wholesale" o "Special"</summary>
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

        /// <summary>Precio Retail vigente. Null si no hay precio configurado.</summary>
        public decimal? RetailPrice { get; set; }

        /// <summary>Todos los precios vigentes de esta unidad.</summary>
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
}