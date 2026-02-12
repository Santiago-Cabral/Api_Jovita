using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.DTOs
{
    public class CreatePublicSaleDto
    {
        [Required(ErrorMessage = "Customer (dirección) es requerido")]
        [MinLength(3, ErrorMessage = "Customer debe tener al menos 3 caracteres")]
        [MaxLength(500, ErrorMessage = "Customer no puede exceder 500 caracteres")]
        public string Customer { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email inválido")]
        [MaxLength(300)]
        public string? Email { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Items es requerido")]
        [MinLength(1, ErrorMessage = "Debe incluir al menos un producto")]
        public List<CreatePublicSaleItemDto> Items { get; set; } = new();

        [Range(0, double.MaxValue, ErrorMessage = "ShippingCost debe ser mayor o igual a 0")]
        public decimal ShippingCost { get; set; } = 0;

        [Required(ErrorMessage = "PaymentMethod es requerido")]
        [RegularExpression(@"^(cash|card|transfer|credit)$",
            ErrorMessage = "PaymentMethod debe ser: cash, card, transfer o credit")]
        public string PaymentMethod { get; set; } = "cash";

        [MaxLength(200, ErrorMessage = "PaymentReference no puede exceder 200 caracteres")]
        public string? PaymentReference { get; set; }

        // NUEVO: fulfillment method (delivery | pickup)
        [RegularExpression(@"(?i)^(delivery|pickup)$", ErrorMessage = "FulfillmentMethod debe ser 'delivery' o 'pickup'")]

        public string FulfillmentMethod { get; set; } = "delivery";
    }
}
