namespace ForrajeriaJovitaAPI.DTOs
{
    public class SaleDto
    {
        public int Id { get; set; }
        public DateTime SoldAt { get; set; }
        public string SellerName { get; set; } = "";
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal Total { get; set; }

        public List<SaleItemDto> Items { get; set; } = new();
        public List<SalePaymentDto> Payments { get; set; } = new();
    }
}
}
