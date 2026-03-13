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
    public class ProductUnitsController : ControllerBase
    {
        private readonly ForrajeriaContext _context;

        public ProductUnitsController(ForrajeriaContext context)
        {
            _context = context;
        }

        // =========================================================
        // GET: api/Products/{id}/units
        // =========================================================
        [HttpGet("{id}/units")]
        public async Task<ActionResult<ProductUnitsResponseDto>> GetProductUnits(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (product == null) return NotFound();

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

            return Ok(new ProductUnitsResponseDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Units = units.Select(MapUnit).ToList()
            });
        }

        // =========================================================
        // GET: api/Products/all-with-units
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
        // POST: api/Products/{id}/units  → crear unidad
        // =========================================================
        [HttpPost("{id}/units")]
        [Authorize]
        public async Task<ActionResult<ProductUnitDto>> CreateUnit(int id, [FromBody] CreateProductUnitDto dto)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (product == null) return NotFound();

            var unit = new ProductUnit
            {
                ProductId = id,
                DisplayName = dto.DisplayName,
                UnitLabel = dto.UnitLabel,
                ConversionToBase = dto.ConversionToBase,
                AllowFractionalQuantity = dto.AllowFractionalQuantity,
                MinSellStep = dto.MinSellStep,
                Barcode = dto.Barcode,
                StockDecimals = dto.StockDecimals,
                CreationDate = DateTime.Now
            };

            _context.ProductUnits.Add(unit);
            await _context.SaveChangesAsync();

            if (dto.RetailPrice.HasValue)
            {
                _context.ProductUnitPrices.Add(new ProductUnitPrice
                {
                    ProductUnitId = unit.Id,
                    Tier = PriceTier.Retail,
                    Price = dto.RetailPrice.Value,
                    StartAt = DateTime.Now,
                    EndAt = DateTime.Now.AddYears(10),
                    CreationDate = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            await _context.Entry(unit).Collection(u => u.ProductUnitPrices).LoadAsync();

            return CreatedAtAction(nameof(GetProductUnits), new { id }, MapUnit(unit));
        }

        // =========================================================
        // PUT: api/Products/{id}/units/{unitId}  → editar unidad
        // =========================================================
        [HttpPut("{id}/units/{unitId}")]
        [Authorize]
        public async Task<ActionResult<ProductUnitDto>> UpdateUnit(int id, int unitId, [FromBody] CreateProductUnitDto dto)
        {
            var unit = await _context.ProductUnits
                .Include(u => u.ProductUnitPrices)
                .FirstOrDefaultAsync(u => u.Id == unitId && u.ProductId == id);

            if (unit == null) return NotFound();

            unit.DisplayName = dto.DisplayName;
            unit.UnitLabel = dto.UnitLabel;
            unit.ConversionToBase = dto.ConversionToBase;
            unit.AllowFractionalQuantity = dto.AllowFractionalQuantity;
            unit.MinSellStep = dto.MinSellStep;
            unit.Barcode = dto.Barcode;
            unit.StockDecimals = dto.StockDecimals;

            if (dto.RetailPrice.HasValue)
            {
                var now = DateTime.Now;

                // Cerrar precios Retail vigentes
                var vigentes = unit.ProductUnitPrices
                    .Where(p => p.Tier == PriceTier.Retail &&
                                (!p.EndAt.HasValue || p.EndAt >= now))
                    .ToList();

                foreach (var v in vigentes)
                    v.EndAt = now;

                _context.ProductUnitPrices.Add(new ProductUnitPrice
                {
                    ProductUnitId = unit.Id,
                    Tier = PriceTier.Retail,
                    Price = dto.RetailPrice.Value,
                    StartAt = now,
                    EndAt = now.AddYears(10),
                    CreationDate = now
                });
            }

            await _context.SaveChangesAsync();
            await _context.Entry(unit).Collection(u => u.ProductUnitPrices).LoadAsync();

            return Ok(MapUnit(unit));
        }

        // =========================================================
        // DELETE: api/Products/{id}/units/{unitId}
        // =========================================================
        [HttpDelete("{id}/units/{unitId}")]
        [Authorize]
        public async Task<IActionResult> DeleteUnit(int id, int unitId)
        {
            var unit = await _context.ProductUnits
                .Include(u => u.ProductUnitPrices)
                .FirstOrDefaultAsync(u => u.Id == unitId && u.ProductId == id);

            if (unit == null) return NotFound();

            _context.ProductUnitPrices.RemoveRange(unit.ProductUnitPrices);
            _context.ProductUnits.Remove(unit);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =========================================================
        // Helpers
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