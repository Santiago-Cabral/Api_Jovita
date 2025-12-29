using System;

namespace ForrajeriaJovitaAPI.Models
{
    public enum PaymentMethod
    {
        Cash = 0,
        Card = 1,
        Credit = 2,
        Transfer = 3
    }

    public class SalePayment
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public Sale? Sale { get; set; }

        public PaymentMethod Method { get; set; }
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;
    }
}
