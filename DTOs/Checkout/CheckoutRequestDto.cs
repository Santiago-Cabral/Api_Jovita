using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.DTOs.Checkout
{
    public class CheckoutRequestDto
    {
        public List<CheckoutItemDto> Items { get; set; } = new();
        public List<CheckoutPaymentDto> Payments { get; set; } = new();
        public decimal Total { get; set; }
        public ClientDto? Client { get; set; }
    }
}