// Models/ProductStock.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class ProductsStock
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public int BranchId { get; set; }

        public decimal Quantity { get; set; } // <── debe ser DECIMAL

        public DateTime CreationDate { get; set; } = DateTime.Now;
    }

}