namespace ForrajeriaJovitaAPI.DTOs
{
    public class UpdateSaleDto
    {
        // 🚚 Datos de envío (opcionales)
        public int? DeliveryType { get; set; }          // 0 = Retiro local, 1 = Envío, etc
        public string? DeliveryAddress { get; set; }
        public decimal? DeliveryCost { get; set; }
        public string? DeliveryNote { get; set; }

        // 💳 Estado del pago / entrega
        // 0 = Pendiente, 1 = Pagado, 2 = Entregado
        public int? PaymentStatus { get; set; }
    }
}
