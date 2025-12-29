using System.ComponentModel.DataAnnotations;

namespace ForrajeriaJovitaAPI.DTOs
{
    public class CreateSaleDto
    {
        [Required]
        public int SellerUserId { get; set; }

        [Required]
        public int CashSessionId { get; set; }

        [Required]
        public List<CreateSaleItemDto> Items { get; set; } = new();

        [Required]
        public List<CreateSalePaymentDto> Payments { get; set; } = new();
    }
}
