// CreatePublicSaleItemDto.cs
namespace ForrajeriaJovitaAPI.DTOs  // ⚠️ DTOs en MAYÚSCULAS
{
    public class CreatePublicSaleItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}

