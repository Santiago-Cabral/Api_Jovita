using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForrajeriaJovitaAPI.Models
{
    [Table("PaymentTransactions")]
    public class PaymentTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SaleId { get; set; }

        [Required]
        [MaxLength(100)]
        public string TransactionId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? CheckoutId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "pending";

        [MaxLength(100)]
        public string? StatusDetail { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "ARS";

        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "card";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        [MaxLength(1000)]
        public string? AdditionalData { get; set; }

        [ForeignKey("SaleId")]
        public virtual Sale? Sale { get; set; }
    }
}