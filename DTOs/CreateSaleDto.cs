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

    public class CreateSaleItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
    }

    public class CreateSalePaymentDto
    {
        public string Method { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }
}