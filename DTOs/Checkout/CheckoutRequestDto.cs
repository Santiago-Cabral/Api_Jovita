using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.DTOs.Checkout
{
    public class CheckoutRequestDto
    {
        public CheckoutClientDto? Client { get; set; }
        public List<CheckoutItemDto> Items { get; set; } = new();
        public List<CheckoutPaymentDto> Payments { get; set; } = new();

        /// <summary>
        /// Total calculado en el frontend (productos + posibles recargos).
        /// El backend lo valida.
        /// </summary>
        public decimal Total { get; set; }
    }
}
