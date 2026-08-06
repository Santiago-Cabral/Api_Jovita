using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs.Coupons;
using ForrajeriaJovitaAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ForrajeriaJovitaAPI.Services
{
    public class CouponService : ICouponService
    {
        private readonly ForrajeriaContext _context;

        public CouponService(ForrajeriaContext context)
        {
            _context = context;
        }

        private static CouponDto MapToDto(Coupon c) => new CouponDto
        {
            Id = c.Id,
            Code = c.Code,
            Type = (int)c.Type,
            TypeName = c.Type == DiscountType.Percentage ? "Porcentaje" : "Monto fijo",
            Value = c.Value,
            MinPurchase = c.MinPurchase,
            MaxUses = c.MaxUses,
            UsedCount = c.UsedCount,
            ExpirationDate = c.ExpirationDate,
            IsActive = c.IsActive,
            CreationDate = c.CreationDate
        };

        public async Task<List<CouponDto>> GetAllAsync()
        {
            var coupons = await _context.Coupons
                .AsNoTracking()
                .OrderByDescending(c => c.CreationDate)
                .ToListAsync();

            return coupons.Select(MapToDto).ToList();
        }

        public async Task<CouponDto?> GetByIdAsync(int id)
        {
            var c = await _context.Coupons.FindAsync(id);
            return c == null ? null : MapToDto(c);
        }

        public async Task<CouponDto> CreateAsync(CouponCreateDto dto)
        {
            var code = dto.Code.Trim().ToUpperInvariant();

            var exists = await _context.Coupons.AnyAsync(c => c.Code == code);
            if (exists)
                throw new InvalidOperationException($"Ya existe un cupón con el código '{code}'");

            var coupon = new Coupon
            {
                Code = code,
                Type = (DiscountType)dto.Type,
                Value = dto.Value,
                MinPurchase = dto.MinPurchase,
                MaxUses = dto.MaxUses,
                ExpirationDate = dto.ExpirationDate,
                IsActive = dto.IsActive,
                UsedCount = 0,
                CreationDate = DateTime.Now
            };

            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            return MapToDto(coupon);
        }

        public async Task<bool> UpdateAsync(CouponUpdateDto dto)
        {
            var coupon = await _context.Coupons.FindAsync(dto.Id);
            if (coupon == null) return false;

            var newCode = dto.Code.Trim().ToUpperInvariant();

            var codeTaken = await _context.Coupons
                .AnyAsync(c => c.Code == newCode && c.Id != dto.Id);
            if (codeTaken)
                throw new InvalidOperationException($"Ya existe un cupón con el código '{newCode}'");

            coupon.Code = newCode;
            coupon.Type = (DiscountType)dto.Type;
            coupon.Value = dto.Value;
            coupon.MinPurchase = dto.MinPurchase;
            coupon.MaxUses = dto.MaxUses;
            coupon.ExpirationDate = dto.ExpirationDate;
            coupon.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return false;

            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ValidateCouponResponseDto> ValidateAsync(string code, decimal cartTotal)
        {
            var normalizedCode = (code ?? string.Empty).Trim().ToUpperInvariant();

            var coupon = await _context.Coupons
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code == normalizedCode);

            if (coupon == null)
                return Invalid("El cupón ingresado no existe");

            if (!coupon.IsActive)
                return Invalid("Este cupón ya no está activo");

            if (coupon.ExpirationDate.HasValue && coupon.ExpirationDate.Value < DateTime.Now)
                return Invalid("Este cupón está vencido");

            if (coupon.MaxUses.HasValue && coupon.UsedCount >= coupon.MaxUses.Value)
                return Invalid("Este cupón alcanzó el límite de usos");

            if (coupon.MinPurchase.HasValue && cartTotal < coupon.MinPurchase.Value)
                return Invalid($"La compra mínima para este cupón es ${coupon.MinPurchase.Value:N0}");

            decimal discount = coupon.Type == DiscountType.Percentage
                ? Math.Round(cartTotal * (coupon.Value / 100m), 2)
                : coupon.Value;

            // El descuento nunca puede superar el total del carrito
            if (discount > cartTotal) discount = cartTotal;
            if (discount < 0) discount = 0;

            return new ValidateCouponResponseDto
            {
                Valid = true,
                Message = "Cupón aplicado correctamente",
                DiscountAmount = discount,
                CouponCode = coupon.Code
            };
        }

        public async Task RegisterUsageAsync(string code)
        {
            var normalizedCode = (code ?? string.Empty).Trim().ToUpperInvariant();
            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c => c.Code == normalizedCode);

            if (coupon == null) return;

            coupon.UsedCount += 1;
            await _context.SaveChangesAsync();
        }

        private static ValidateCouponResponseDto Invalid(string message) => new ValidateCouponResponseDto
        {
            Valid = false,
            Message = message,
            DiscountAmount = 0,
            CouponCode = string.Empty
        };
    }
}