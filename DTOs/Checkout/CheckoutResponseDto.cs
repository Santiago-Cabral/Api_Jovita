using System;

namespace ForrajeriaJovitaAPI.DTOs.Checkout
{
    public class CheckoutResponseDto
    {
        public int SaleId { get; set; }

        public decimal Subtotal { get; set; }

        public decimal DiscountTotal { get; set; }

        public DateTime SoldAt { get; set; }

        public bool StockActualizado { get; set; }

        public string? TicketUrl { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
