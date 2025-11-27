namespace ForrajeriaJovitaAPI.DTOs
{
    public class SalePaymentDto
    {
        public int Method { get; set; }
        public string MethodName { get; set; } = "";
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }
}
