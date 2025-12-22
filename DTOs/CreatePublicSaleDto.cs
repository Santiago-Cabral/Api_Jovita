namespace ForrajeriaJovitaAPI.DTOs
{
    public class CreatePublicSaleDto
    {
        public PublicClientDto Client { get; set; } = new();

        public List<PublicSaleItemDto> Items { get; set; } = new();

        public decimal ShippingCost { get; set; }

        // "mercado_pago" | "cash"
        public string PaymentMethod { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }
    }

    public class PublicClientDto
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class PublicSaleItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
