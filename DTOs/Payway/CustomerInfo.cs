// DTOs/Payway/CustomerInfo.cs
namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    /// <summary>
    /// Información del cliente para el pago
    /// </summary>
    public class CustomerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }
}