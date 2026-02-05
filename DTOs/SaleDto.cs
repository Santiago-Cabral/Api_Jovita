using System;
using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.DTOs
{
    public class SaleItemDto
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
    }

    public class SalePaymentDto
    {
        public string? MethodName { get; set; }
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }

    public class SaleDto
    {
        public int Id { get; set; }
        public DateTime SoldAt { get; set; }

        public string SellerName { get; set; } = string.Empty;

        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal Total { get; set; }

        // Cliente
        public int? ClientId { get; set; }
        public string? ClientName { get; set; }

        // 🚚 Datos de envío (opcionales)
        public int? DeliveryType { get; set; }
        public string? DeliveryAddress { get; set; }
        public decimal? DeliveryCost { get; set; }
        public string? DeliveryNote { get; set; }

        // 💳 Estado del pago / entrega
        public int? PaymentStatus { get; set; }
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
