// DTOs/SaleDto.cs
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
        public List<SaleItemDto> Items { get; set; } = new();
        public List<SalePaymentDto> Payments { get; set; } = new();
    }

    public class SaleItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
    }

    public class SalePaymentDto
    {
        public int Method { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }

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
        public int? ProductUnitId { get; set; }
        public decimal Quantity { get; set; }
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