// ============================================
// StockDto.cs
// ============================================
namespace ForrajeriaJovitaAPI.DTOs
{
    public class StockDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}