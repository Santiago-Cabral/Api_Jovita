namespace ForrajeriaJovitaAPI.DTOs.Checkout
{
    public class CheckoutResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        // Venta / Checkout
        public int? VentaId { get; set; }
        public decimal? Total { get; set; }

        // Payway
        public bool RequiresPayment { get; set; }
        public string? PaywayRedirectUrl { get; set; }

        // Para errores externos
        public string? ErrorCode { get; set; }
    }
}
