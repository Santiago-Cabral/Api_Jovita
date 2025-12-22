// CreatePublicSaleDto.cs
namespace ForrajeriaJovitaAPI.DTOs  // ?? DTOs en MAYÚSCULAS
{
    public class CreatePublicSaleDto
    {
        public string Customer { get; set; } = string.Empty;
        public List<CreatePublicSaleItemDto> Items { get; set; } = new();
        public decimal ShippingCost { get; set; } = 0;
        // cash | card | credit | transfer
        public string PaymentMethod { get; set; } = "cash";
        // Referencia Payway (opcional)
        public string? PaymentReference { get; set; }
    }
}