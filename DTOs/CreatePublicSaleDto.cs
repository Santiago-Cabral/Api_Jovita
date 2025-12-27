// DTOs/CreatePublicSaleDto.cs
using System.ComponentModel.DataAnnotations;

namespace ForrajeriaJovitaAPI.DTOs
{
    /// <summary>
    /// DTO para crear una venta pública desde el carrito web
    /// No requiere autenticación - endpoint público
    /// </summary>
    public class CreatePublicSaleDto
    {
        /// <summary>
        /// Dirección de entrega del cliente
        /// Ejemplo: "Av. Belgrano 123, Tucumán"
        /// </summary>
        [Required(ErrorMessage = "Customer (dirección) es requerido")]
        [MinLength(3, ErrorMessage = "Customer debe tener al menos 3 caracteres")]
        [MaxLength(500, ErrorMessage = "Customer no puede exceder 500 caracteres")]
        public string Customer { get; set; } = string.Empty;

        /// <summary>
        /// Lista de productos en el carrito
        /// Debe contener al menos un producto
        /// </summary>
        [Required(ErrorMessage = "Items es requerido")]
        [MinLength(1, ErrorMessage = "Debe incluir al menos un producto")]
        public List<CreatePublicSaleItemDto> Items { get; set; } = new();

        /// <summary>
        /// Costo de envío/delivery
        /// Default: 0
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "ShippingCost debe ser mayor o igual a 0")]
        public decimal ShippingCost { get; set; } = 0;

        /// <summary>
        /// Método de pago seleccionado
        /// Valores válidos: "cash", "card", "transfer", "credit"
        /// </summary>
        [Required(ErrorMessage = "PaymentMethod es requerido")]
        [RegularExpression(@"^(cash|card|transfer|credit)$",
            ErrorMessage = "PaymentMethod debe ser: cash, card, transfer o credit")]
        public string PaymentMethod { get; set; } = "cash";

        /// <summary>
        /// Referencia de pago (número de transacción, comprobante, etc.)
        /// Opcional - usado para tracking de pagos
        /// </summary>
        [MaxLength(200, ErrorMessage = "PaymentReference no puede exceder 200 caracteres")]
        public string? PaymentReference { get; set; }
    }
}