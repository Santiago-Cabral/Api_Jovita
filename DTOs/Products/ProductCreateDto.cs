namespace ForrajeriaJovitaAPI.DTOs.Products
{
    public class ProductCreateDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }

        public int BaseUnit { get; set; } // 1=kg, 2=unidad, 3=litro

        public string? Image { get; set; }
        public int? CategoryId { get; set; }
    }
}
