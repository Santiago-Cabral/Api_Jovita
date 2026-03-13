using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs.ProductUnits;
using ForrajeriaJovitaAPI.Models;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/Products")]
    [AllowAnonymous]
    public class ProductUnitsController : ControllerBase
    {
        private readonly ForrajeriaContext _context;

        public ProductUnitsController(ForrajeriaContext context)
        {
            _context = context;
        }

        // =========================================================
        // GET: api/Products/{id}/units
        // Unidades de venta de un producto con precios vigentes
        // =========================================================
        [HttpGet("{id}/units")]
        public async Task<ActionResult<ProductUnitsResponseDto>> GetProductUnits(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (product == null)
                return NotFound();

            var now = DateTime.Now;

            var units = await _context.ProductUnits
                .Where(u => u.ProductId == id)
                .Include(u => u.ProductUnitPrices
                    .Where(p =>
                        (!p.StartAt.HasValue || p.StartAt <= now) &&
                        (!p.EndAt.HasValue || p.EndAt >= now)
                    ))
                .AsNoTracking()
                .OrderBy(u => u.ConversionToBase)
                .ToListAsync();

            var result = new ProductUnitsResponseDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Units = units.Select(u => MapUnit(u)).ToList()
            };

            return Ok(result);
        }

        // =========================================================
        // GET: api/Products/all-with-units
        // Catálogo web: productos activos con precio Retail de unidad base
        // =========================================================
        [HttpGet("all-with-units")]
        public async Task<ActionResult<IEnumerable<ProductWithBaseUnitDto>>> GetAllWithUnits()
        {
            var now = DateTime.Now;

            var products = await _context.Products
                .Where(p => !p.IsDeleted && p.IsActived)
                .Include(p => p.Category)
                .Include(p => p.ProductUnits)
                    .ThenInclude(u => u.ProductUnitPrices
                        .Where(pr =>
                            (!pr.StartAt.HasValue || pr.StartAt <= now) &&
                            (!pr.EndAt.HasValue || pr.EndAt >= now)
                        ))
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync();

            var result = products.Select(p =>
            {
                // Unidad base: la de mayor ConversionToBase (ej: bolsa 20kg = 20, vs 1kg = 1)
                var baseUnit = p.ProductUnits
                    .OrderByDescending(u => u.ConversionToBase)
                    .FirstOrDefault();

                var retailPrice = baseUnit?.ProductUnitPrices
                    .Where(pr => pr.Tier == PriceTier.Retail)
                    .OrderByDescending(pr => pr.StartAt)
                    .FirstOrDefault()?.Price;

                return new ProductWithBaseUnitDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    Image = p.Image,
                    CategoryId = p.CategoryId ?? 0,
                    CategoryName = p.Category?.Name,
                    IsFeatured = p.IsFeatured,
                    BaseUnitLabel = baseUnit?.UnitLabel ?? "",
                    BaseUnitDisplayName = baseUnit?.DisplayName ?? "",
                    BaseRetailPrice = retailPrice,
                    UnitCount = p.ProductUnits.Count
                };
            }).ToList();

            return Ok(result);
        }

        // =========================================================
        // Helpers privados
        // =========================================================
        private static ProductUnitDto MapUnit(ProductUnit u) => new()
        {
            Id = u.Id,
            DisplayName = u.DisplayName,
            UnitLabel = u.UnitLabel,
            ConversionToBase = u.ConversionToBase,
            AllowFractionalQuantity = u.AllowFractionalQuantity,
            MinSellStep = u.MinSellStep,
            Barcode = u.Barcode,
            StockDecimals = u.StockDecimals,
            RetailPrice = u.ProductUnitPrices
                .Where(p => p.Tier == PriceTier.Retail)
                .OrderByDescending(p => p.StartAt)
                .FirstOrDefault()?.Price,
            Prices = u.ProductUnitPrices
                .OrderBy(p => p.Tier)
                .Select(p => new ProductUnitPriceDto
                {
                    Id = p.Id,
                    Tier = p.Tier.ToString(),
                    TierValue = (int)p.Tier,
                    Price = p.Price
                }).ToList()
        };
    }
}