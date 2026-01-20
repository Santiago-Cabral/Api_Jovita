namespace ForrajeriaJovitaAPI.Models.DTOs
{
    // Esta es la clase que el compilador no encuentra
    public class CreateCheckoutResponse
    {
        public string id { get; set; }
        public string checkout_url { get; set; }
        public string type { get; set; }
        public string status { get; set; }
    }

    // Opcional: Estructura para el objeto de pago
    public class PaymentRequest
    {
        public decimal amount { get; set; }
        public string currency { get; set; } = "ARS";
        // Agrega aquí los campos que envías a Payway
    }
}