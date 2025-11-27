namespace ForrajeriaJovitaAPI.DTOs
{
    public class UpdateSaleDto
    {
        public int? DeliveryType { get; set; }
        public string? DeliveryAddress { get; set; }
        public decimal? DeliveryCost { get; set; }
        public string? DeliveryNote { get; set; }
        public int? PaymentStatus { get; set; }
    }
}
