using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs.Products;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous] // 👈 ESTA LÍNEA
    public class ProductsController : ControllerBase

    {
        private readonly ForrajeriaContext _context;
        private readonly IStockService _stockService;

        public ProductsController(ForrajeriaContext context, IStockService stockService)
        {
            _context = context;
            _stockService = stockService;
        }

        // =========================================================
        // GET: api/Products  -> lista con Stock total
        // =========================================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetProducts()
        {
            // 1) Traer productos + categoría
            var products = await _context.Products
                .Where(p => !p.IsDeleted)
                .Include(p => p.Category)
                .AsNoTracking()
                .ToListAsync();

            var productIds = products.Select(p => p.Id).ToList();

            // 2) Diccionario de stock (por defecto 0)
            var stockDict = new Dictionary<int, decimal>();

            try
            {
                // Intentar leer el stock; si la tabla no existe o hay error, caemos al catch
                var stockRows = await _context.ProductsStocks
                    .Where(s => productIds.Contains(s.ProductId))
                    .AsNoTracking()
                    .ToListAsync();

                stockDict = stockRows
                    .GroupBy(s => s.ProductId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(x => x.Quantity)
                    );
            }
            catch
            {
                // NO lanzamos la excepción: devolvemos productos con Stock = 0.
                // Si querés loguear algo acá podés usar un logger.
            }

            // 3) Mapear a DTO
            var result = products.Select(p => new ProductResponseDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                CostPrice = p.CostPrice,
                RetailPrice = p.RetailPrice,
                WholesalePrice = p.WholesalePrice,
                BaseUnit = (int)p.BaseUnit,
                IsActived = p.IsActived,
                UpdateDate = p.UpdateDate ?? p.CreationDate,
                CategoryId = p.CategoryId ?? 0,
                CategoryName = p.Category != null ? p.Category.Name : null,
                Image = p.Image,
                Stock = stockDict.TryGetValue(p.Id, out var qty) ? qty : 0
            }).ToList();

            return Ok(result);
        }

        // =========================================================
        // GET: api/Products/5  -> un producto con Stock total
        // =========================================================
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductResponseDto>> GetProduct(int id)
        {
            var p = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (p == null)
                return NotFound();

            decimal stockTotal = 0;

            try
            {
                stockTotal = await _context.ProductsStocks
                    .Where(s => s.ProductId == id)
                    .SumAsync(s => s.Quantity);
            }
            catch
            {
                // Si hay error de stock, devolvemos 0 pero no rompemos el endpoint
                stockTotal = 0;
            }

            var dto = new ProductResponseDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                CostPrice = p.CostPrice,
                RetailPrice = p.RetailPrice,
                WholesalePrice = p.WholesalePrice,
                BaseUnit = (int)p.BaseUnit,
                IsActived = p.IsActived,
                UpdateDate = p.UpdateDate ?? p.CreationDate,
                CategoryId = p.CategoryId ?? 0,
                CategoryName = p.Category != null ? p.Category.Name : null,
                Image = p.Image,
                Stock = stockTotal
            };

            return Ok(dto);
        }

        // =========================================================
        // POST: api/Products  -> crear producto
        // =========================================================
        [HttpPost]
        public async Task<ActionResult<ProductResponseDto>> CreateProduct([FromBody] ProductCreateDto dto)
        {
            var product = new Product
            {
                Code = dto.Code,
                Name = dto.Name,
                CostPrice = dto.CostPrice,
                RetailPrice = dto.RetailPrice,
                WholesalePrice = dto.WholesalePrice,
                BaseUnit = (BaseUnit)dto.BaseUnit,
                Image = dto.Image,
                CategoryId = dto.CategoryId,
                IsActived = dto.IsActived,
                IsDeleted = false,
                CreationDate = DateTime.Now
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            var response = new ProductResponseDto
            {
                Id = product.Id,
                Code = product.Code,
                Name = product.Name,
                CostPrice = product.CostPrice,
                RetailPrice = product.RetailPrice,
                WholesalePrice = product.WholesalePrice,
                BaseUnit = (int)product.BaseUnit,
                IsActived = product.IsActived,
                UpdateDate = product.UpdateDate ?? product.CreationDate,
                CategoryId = product.CategoryId ?? 0,
                CategoryName = null,
                Image = product.Image,
                Stock = 0 // recién creado, sin stock aún
            };

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, response);
        }

        // =========================================================
        // PUT: api/Products/5  -> actualizar
        // =========================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductUpdateDto dto)
        {
            if (id != dto.Id)
                return BadRequest("El Id de la URL no coincide con el del body");

            var product = await _context.Products.FindAsync(id);
            if (product == null || product.IsDeleted)
                return NotFound();

            product.Code = dto.Code;
            product.Name = dto.Name;
            product.CostPrice = dto.CostPrice;
            product.RetailPrice = dto.RetailPrice;
            product.WholesalePrice = dto.WholesalePrice;
            product.BaseUnit = (BaseUnit)dto.BaseUnit;
            product.Image = dto.Image;
            product.CategoryId = dto.CategoryId;
            product.IsActived = dto.IsActived;
            product.UpdateDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // =========================================================
        // DELETE lógico: api/Products/5
        // =========================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null || product.IsDeleted)
                return NotFound();

            product.IsDeleted = true;
            product.IsActived = false;
            product.UpdateDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // =========================================================
        // ENDPOINTS DE STOCK (por producto)
        // =========================================================

        // GET api/Products/5/stock -> lista stock por sucursal
        [HttpGet("{id}/stock")]
        public async Task<IActionResult> GetStockByProduct(int id)
        {
            var stocks = await _stockService.GetStockByProductAsync(id);
            return Ok(stocks);
        }

        // POST api/Products/5/stock/set  (setea cantidad exacta)
        [HttpPost("{id}/stock/set")]
        public async Task<IActionResult> SetStock(int id, [FromBody] UpdateStockDto dto)
        {
            if (dto.BranchId <= 0)
                return BadRequest("BranchId inválido");

            dto.ProductId = id;

            await _stockService.UpdateStockAsync(dto);
            return Ok(new { message = "Stock actualizado correctamente" });
        }

        // POST api/Products/5/stock/add  (suma stock)
        [HttpPost("{id}/stock/add")]
        public async Task<IActionResult> AddStock(int id, [FromBody] UpdateStockDto dto)
        {
            if (dto.BranchId <= 0)
                return BadRequest("BranchId inválido");

            dto.ProductId = id;

            await _stockService.AddStockAsync(dto);
            return Ok(new { message = "Stock agregado correctamente" });
        }
    }
}

