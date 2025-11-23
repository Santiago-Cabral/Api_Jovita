namespace ForrajeriaJovitaAPI.DTOs.Products
{
    public class ProductResponseDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }

        public string? Image { get; set; }
        public string? CategoryName { get; set; }

        public int Stock { get; set; }
    }
}
