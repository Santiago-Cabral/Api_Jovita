namespace ForrajeriaJovitaAPI.DTOs.Products
{
    public class ProductStockDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
