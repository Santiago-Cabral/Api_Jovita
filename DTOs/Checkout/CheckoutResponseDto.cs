using System;
using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.DTOs.Checkout
{
    public class CheckoutResponseDto
    {
        public int SaleId { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal Total { get; set; }
        public DateTime SoldAt { get; set; }
        public List<CheckoutStockDto> StockActualizado { get; set; } = new();
        public string? TicketUrl { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? PaywayRedirectUrl { get; set; }
    }
}