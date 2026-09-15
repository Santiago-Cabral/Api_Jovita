namespace ForrajeriaJovitaAPI.DTOs.MercadoPago
{
    public class CreateCheckoutRequest
    {
        public int SaleId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public CustomerInfo? Customer { get; set; }
    }

    public class CustomerInfo
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }
}