namespace ForrajeriaJovitaAPI.DTOs.Products
{
    public class ProductCreateDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }

        public int BaseUnit { get; set; }

        public string? Image { get; set; }
        public int CategoryId { get; set; }

        public bool IsActived { get; set; } = true;
        public bool IsFeatured { get; set; } = false;
    }
}
