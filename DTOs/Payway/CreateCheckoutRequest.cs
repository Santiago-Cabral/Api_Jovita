// DTOs/Payway/CreateCheckoutRequest.cs
using System.ComponentModel.DataAnnotations;

namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    /// <summary>
    /// Request para crear un checkout de Payway desde el frontend
    /// </summary>
    public class CreateCheckoutRequest
    {
        [Required]
        public int SaleId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero")]
        public decimal Amount { get; set; }

        public string? Description { get; set; }

        [Required]
        public CustomerInfo Customer { get; set; } = null!;

        public string? ReturnUrl { get; set; }

        public string? CancelUrl { get; set; }
    }
}