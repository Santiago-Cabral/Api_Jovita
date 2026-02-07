namespace ForrajeriaJovitaAPI.DTOs
{
    public class UpdateSaleDto
    {
        public int? PaymentStatus { get; set; }
        public decimal? DeliveryCost { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? PaymentMethod { get; set; }
        public string? FulfillmentMethod { get; set; }
        // agregar ClientId si hace falta: public int? ClientId { get; set; }
    }
}
