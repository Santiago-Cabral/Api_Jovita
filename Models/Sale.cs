using System;
using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.Models
{
    public class Sale
    {
        public int Id { get; set; }

        // FK hacia CashMovements
        public int CashMovementId { get; set; }
        public CashMovement? CashMovement { get; set; }

        public int? ClientId { get; set; }
        public Client? Client { get; set; }

        public int SellerUserId { get; set; }
        public User? SellerUser { get; set; }

        public DateTime SoldAt { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal Total { get; set; }

        public int? DeliveryType { get; set; }
        public string? DeliveryAddress { get; set; }
        public decimal? DeliveryCost { get; set; }
        public string? DeliveryNote { get; set; }

        // 🔧 FIX: Mantener como INT para coincidir con la base de datos
        public int PaymentStatus { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.Now;

        public virtual ICollection<SaleItem> SalesItems { get; set; } = new List<SaleItem>();
        public virtual ICollection<SalePayment> SalesPayments { get; set; } = new List<SalePayment>();
    }
}