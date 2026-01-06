// DTOs/Checkout/CheckoutResponseDto.cs
using System;
using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.DTOs.Checkout
{
   

    public class CheckoutResponseDto
    {
        // Identificador de la venta (sale.Id)
        public int SaleId { get; set; }

        // Totales y montos
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal Total { get; set; }

        // Fecha/hora de la venta
        public DateTime SoldAt { get; set; }

        // Estado del stock por producto tras la venta
        public List<CheckoutStockDto> StockActualizado { get; set; } = new();

        // URL al ticket si aplica
        public string? TicketUrl { get; set; }

        // Mensaje para el frontend
        public string Message { get; set; } = string.Empty;

        // (opcional) URL de redirección a Payway devuelta por el provider
        public string? PaywayRedirectUrl { get; set; }
    }
}
