// Models/SalePayment.cs
namespace ForrajeriaJovitaAPI.Models
{
    public enum PaymentMethod
    {
        Cash = 1,
        Card = 2,
        Transfer = 3,
        Credit = 4
    }

    public class SalePayment
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public PaymentMethod Method { get; set; }
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public Sale Sale { get; set; } = null!;
    }
}