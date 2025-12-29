using System.ComponentModel.DataAnnotations;

namespace ForrajeriaJovitaAPI.DTOs
{
    public class CreateSaleItemDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        public decimal Discount { get; set; } = 0m;

        public int? ProductUnitId { get; set; }
    }
}
