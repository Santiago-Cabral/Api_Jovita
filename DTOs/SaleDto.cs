namespace ForrajeriaJovitaAPI.DTOs
{
    public class SaleDto
    {
        public int Id { get; set; }
        public DateTime SoldAt { get; set; }
        public string SellerName { get; set; } = string.Empty;

        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal Total { get; set; }

        public int? DeliveryType { get; set; }
        public string? DeliveryAddress { get; set; }
        public decimal? DeliveryCost { get; set; }
        public string? DeliveryNote { get; set; }
        public int PaymentStatus { get; set; }

        public List<SaleItemDto> Items { get; set; } = new();
        public List<SalePaymentDto> Payments { get; set; } = new();
    }
}
