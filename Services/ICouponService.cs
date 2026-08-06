using System.Collections.Generic;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.DTOs.Coupons;

namespace ForrajeriaJovitaAPI.Services
{
    public interface ICouponService
    {
        Task<List<CouponDto>> GetAllAsync();
        Task<CouponDto?> GetByIdAsync(int id);
        Task<CouponDto> CreateAsync(CouponCreateDto dto);
        Task<bool> UpdateAsync(CouponUpdateDto dto);
        Task<bool> DeleteAsync(int id);

        // Valida el código contra el total del carrito, sin consumir el uso todavía
        Task<ValidateCouponResponseDto> ValidateAsync(string code, decimal cartTotal);

        // Se llama SOLO cuando la venta se confirma, para incrementar UsedCount
        Task RegisterUsageAsync(string code);
    }
}