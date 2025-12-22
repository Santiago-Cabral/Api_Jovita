using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.Dtos
{
    public class CreatePublicSaleDto
    {
        public string Customer { get; set; } = string.Empty;
        public List<CreatePublicSaleItemDto> Items { get; set; } = new();

        // cash | card | credit | transfer
        public string PaymentMethod { get; set; } = "cash";

        // Referencia Payway (opcional)
        public string? PaymentReference { get; set; }
    }
}
