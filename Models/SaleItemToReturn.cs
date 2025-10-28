// Models/SaleItemToReturn.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class SaleItemToReturn
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;
    }
}