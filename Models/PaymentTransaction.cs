using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForrajeriaJovitaAPI.Models
{
    /// <summary>
    /// Registro de transacción de pago con Payway
    /// </summary>
    [Table("PaymentTransactions")]
    public class PaymentTransaction
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID de la venta asociada
        /// </summary>
        [Required]
        public int SaleId { get; set; }

        [ForeignKey(nameof(SaleId))]
        public Sale? Sale { get; set; }

        /// <summary>
        /// ID de transacción único generado por nuestra app (site_transaction_id en Payway)
        /// Formato: JOV_20250113123456_98
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string TransactionId { get; set; } = null!;

        /// <summary>
        /// Hash del checkout de Payway
        /// </summary>
        [MaxLength(100)]
        public string? CheckoutId { get; set; }

        /// <summary>
        /// Monto de la transacción
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Moneda (siempre ARS)
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "ARS";

        /// <summary>
        /// Estado del pago: pending, approved, rejected, cancelled
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "pending";

        /// <summary>
        /// Detalle del estado (ej: "tarjeta rechazada")
        /// </summary>
        [MaxLength(200)]
        public string? StatusDetail { get; set; }

        /// <summary>
        /// Proveedor de pago (siempre "payway")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Provider { get; set; } = "payway";

        /// <summary>
        /// Método de pago (card, transfer, etc.)
        /// </summary>
        [MaxLength(50)]
        public string? PaymentMethod { get; set; } = "card";

        /// <summary>
        /// Fecha de creación de la transacción
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha de última actualización
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha en que se completó el pago
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Respuesta cruda de Payway (para debugging)
        /// </summary>
        [MaxLength(500)]
        public string? RawResponse { get; set; }
    }
}