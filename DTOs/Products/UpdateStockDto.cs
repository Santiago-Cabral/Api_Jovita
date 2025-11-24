// ============================================
// UpdateStockDto.cs
// ============================================
namespace ForrajeriaJovitaAPI.DTOs.Products
{
    public class UpdateStockDto
    {
        public int ProductId { get; set; }
        public int BranchId { get; set; }
        public decimal Quantity { get; set; }
    }
}