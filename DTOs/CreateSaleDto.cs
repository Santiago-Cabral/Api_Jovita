namespace ForrajeriaJovitaAPI.DTOs
{
    public class CreateSaleDto
    {
        public int SellerUserId { get; set; }
        public int CashSessionId { get; set; }
        public List<CreateSaleItemDto> Items { get; set; } = new();
        public List<CreateSalePaymentDto> Payments { get; set; } = new();
    }

    public class CreateSaleItemDto
    {
        public int ProductId { get; set; }
        public int ProductUnitId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
    }

    public class CreateSalePaymentDto
    {
        public int Method { get; set; }
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }
}
