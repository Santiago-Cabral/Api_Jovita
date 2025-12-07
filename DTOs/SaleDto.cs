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

        // 🚚 Datos de envío (opcionales)
        public int? DeliveryType { get; set; }          // 0 = Retiro local, 1 = Envío, etc (según tu enum / lógica)
        public string? DeliveryAddress { get; set; }
        public decimal? DeliveryCost { get; set; }
        public string? DeliveryNote { get; set; }

        // 💳 Estado del pago / entrega
        // 0 = Pendiente, 1 = Pagado, 2 = Entregado (o como lo manejes)
        public int? PaymentStatus { get; set; }

        // Nombre legible para el front
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
