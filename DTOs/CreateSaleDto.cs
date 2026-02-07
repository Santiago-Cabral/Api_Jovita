using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ForrajeriaJovitaAPI.DTOs
{
    public class CreateSaleDto
    {
        // Id del cliente si la venta la hace un cliente registrado (opcional)
        public int? ClientId { get; set; }

        [Required]
        public List<CreatePublicSaleItemDto> Items { get; set; } = new();

        [Range(0, double.MaxValue)]
        public decimal ShippingCost { get; set; } = 0;

        // método de pago interno: cash | card | transfer
        [Required]
        [RegularExpression(@"^(cash|card|transfer|credit)$")]
        public string PaymentMethod { get; set; } = "cash";

        public string? PaymentReference { get; set; }

        // fulfillment
        [RegularExpression(@"^(delivery|pickup)$")]
        public string FulfillmentMethod { get; set; } = "delivery";

        // Delivery address opcional (para envíos)
        public string? DeliveryAddress { get; set; }
    }
}
