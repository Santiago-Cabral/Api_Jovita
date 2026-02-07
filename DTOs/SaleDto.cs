using System;
using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.DTOs
{
    public class SaleDto
    {
        public int Id { get; set; }
        public DateTime SoldAt { get; set; }
        public string SellerName { get; set; } = string.Empty;

        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal Total { get; set; }

        // Nombre que viene desde el checkout / cliente
        public string? CustomerName { get; set; }

        // Cliente (si la venta está asociada a un cliente registrado)
        public int? ClientId { get; set; }
        public string? ClientName { get; set; }

        // Entrega
        public int? DeliveryType { get; set; }              // si tu modelo lo usa
        public string? DeliveryAddress { get; set; }
        public decimal? DeliveryCost { get; set; }
        public string? DeliveryNote { get; set; }

        // Delivery / Pickup en string (más legible en frontend)
        public string? FulfillmentMethod { get; set; }      // e.g. "delivery" | "pickup"

        // Pago
        public int? PaymentStatus { get; set; }             // 0=pendiente,1=pagado,2=entregado
        public string? PaymentMethod { get; set; }          // e.g. "cash" | "transfer" | "card"
        public string PaymentStatusName =>
            PaymentStatus switch
            {
                0 => "Pendiente",
                1 => "Pagado",
                2 => "Entregado",
                _ => "Desconocido"
            };

        public List<SaleItemDto> Items { get; set; } = new();
        public List<SalePaymentDto> Payments { get; set; } = new();
    }
}
