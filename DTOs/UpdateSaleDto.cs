public class UpdateSaleDto
{
    public int? PaymentStatus { get; set; }
    public decimal? DeliveryCost { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? PaymentMethod { get; set; }
    public string? FulfillmentMethod { get; set; }

    // Nuevas opciones de administración
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
}
