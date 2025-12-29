using System.ComponentModel.DataAnnotations;

namespace ForrajeriaJovitaAPI.DTOs
{
    public class CreateSalePaymentDto
    {
        [Required]
        public int Method { get; set; } // mapear al enum PaymentMethod en el servicio

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }

        public string? Reference { get; set; }
    }
}
