using System;

namespace ForrajeriaJovitaAPI.DTOs.Coupons
{
    public class CouponDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public int Type { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public decimal? MinPurchase { get; set; }
        public int? MaxUses { get; set; }
        public int UsedCount { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreationDate { get; set; }
    }

    public class CouponCreateDto
    {
        public string Code { get; set; } = string.Empty;
        public int Type { get; set; } // 0 = Percentage, 1 = Fixed
        public decimal Value { get; set; }
        public decimal? MinPurchase { get; set; }
        public int? MaxUses { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CouponUpdateDto : CouponCreateDto
    {
        public int Id { get; set; }
    }

    // Lo que manda el checkout para validar un código
    public class ValidateCouponRequestDto
    {
        public string Code { get; set; } = string.Empty;
        public decimal CartTotal { get; set; }
    }

    // Lo que devuelve el backend al checkout
    public class ValidateCouponResponseDto
    {
        public bool Valid { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public string CouponCode { get; set; } = string.Empty;
    }
}