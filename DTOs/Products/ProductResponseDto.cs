namespace ForrajeriaJovitaAPI.DTOs.Products
{
    public class ProductResponseDto
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }

        public int BaseUnit { get; set; }

        public bool IsActived { get; set; }
        public DateTime UpdateDate { get; set; }

        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }

        public string? Image { get; set; }

        public int Stock { get; set; }
    }
}
