namespace ForrajeriaJovitaAPI.DTOs
{
    public class SaleItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
    }
}
