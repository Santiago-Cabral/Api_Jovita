using System;

namespace ForrajeriaJovitaAPI.Models
{
    public enum DiscountType
    {
        Percentage = 0,
        Fixed = 1
    }

    public class Coupon
    {
        public int Id { get; set; }

        // Se guarda siempre en MAYÚSCULAS para comparar sin ambigüedad
        public string Code { get; set; } = string.Empty;

        public DiscountType Type { get; set; } = DiscountType.Percentage;

        // Si Type = Percentage, Value es 0-100. Si Type = Fixed, Value es un monto en $
        public decimal Value { get; set; }

        public decimal? MinPurchase { get; set; }

        // null = usos ilimitados
        public int? MaxUses { get; set; }

        public int UsedCount { get; set; } = 0;

        public DateTime? ExpirationDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreationDate { get; set; } = DateTime.Now;
    }
}