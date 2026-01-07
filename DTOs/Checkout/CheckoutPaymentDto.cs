namespace ForrajeriaJovitaAPI.DTOs.Checkout
{
    public class CheckoutPaymentDto
    {
        public int Method { get; set; }
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }
}