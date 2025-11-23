namespace ForrajeriaJovitaAPI.DTOs.Checkout
{
    public class CheckoutPaymentDto
    {
        /// <summary>
        /// 1 = Efectivo, 2 = Transferencia, 3 = Débito, 4 = Crédito
        /// </summary>
        public int Method { get; set; }
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }
}
