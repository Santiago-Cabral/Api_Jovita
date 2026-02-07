using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.DTOs
{
    public class CreateSaleDto
    {
        public int? ClientId { get; set; }
        public List<CreateSaleItemDto> Items { get; set; } = new List<CreateSaleItemDto>();

        // ✅ PROPIEDAD AGREGADA PARA FIX
        public List<CreateSalePaymentDto>? Payments { get; set; }
    }

    
}