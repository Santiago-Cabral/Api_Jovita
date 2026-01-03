// Models/PaymentTransaction.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForrajeriaJovitaAPI.Models
{
    /// <summary>
    /// Modelo para guardar las transacciones de pago con Payway
    /// </summary>
    [Table("PaymentTransactions")]
    public class PaymentTransaction
    {
        /// <summary>
        /// ID único de la transacción en nuestra base de datos
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID de la venta asociada (Foreign Key hacia Sales)
        /// </summary>
        [Required]
        public int SaleId { get; set; }

        /// <summary>
        /// ID único de la transacción generado por nosotros (TXN-123-456789)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string TransactionId { get; set; } = string.Empty;

        /// <summary>
        /// ID del checkout generado por Payway
        /// </summary>
        [MaxLength(100)]
        public string? CheckoutId { get; set; }

        /// <summary>
        /// Estado del pago: pending, approved, rejected, cancelled
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "pending";

        /// <summary>
        /// Detalle adicional del estado (ej: "insufficient_amount", "invalid_card")
        /// </summary>
        [MaxLength(100)]
        public string? StatusDetail { get; set; }

        /// <summary>
        /// Monto total de la transacción
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Moneda (siempre ARS para Argentina)
        /// </summary>
        [MaxLength(10)]
        public string Currency { get; set; } = "ARS";

        /// <summary>
        /// Método de pago (siempre "card" para Payway)
        /// </summary>
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "card";

        /// <summary>
        /// Fecha y hora de creación de la transacción
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha y hora de última actualización
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Fecha y hora en que se completó el pago (cuando status = approved)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Datos adicionales en formato JSON (opcional)
        /// </summary>
        [MaxLength(1000)]
        public string? AdditionalData { get; set; }

        /// <summary>
        /// Navegación hacia la venta asociada
        /// </summary>
        [ForeignKey("SaleId")]
        public virtual Sale? Sale { get; set; }
    }
}