// DTOs/ProductDto.cs
namespace ForrajeriaJovitaAPI.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public bool IsActived { get; set; }
        public string BaseUnit { get; set; } = string.Empty;
        public DateTime? UpdateDate { get; set; }
    }

  
}



