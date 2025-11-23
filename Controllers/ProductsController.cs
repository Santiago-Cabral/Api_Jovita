using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs.Products;
using ForrajeriaJovitaAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ForrajeriaContext _context;

        public ProductsController(ForrajeriaContext context)
        {
            _context = context;
        }

        // ===================================
        // CLIENTE WEB - CATÁLOGO
        // ===================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Select(p => new ProductResponseDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    RetailPrice = p.RetailPrice,
                    WholesalePrice = p.WholesalePrice,
                    Image = p.Image,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    Stock = p.ProductsStocks.Sum(s => (int)s.Quantity)
                })
                .ToListAsync();

            return Ok(products);
        }

        // ===================================
        // ADMIN
        // ===================================
        [Authorize(Roles = "administrador/a")]
        [HttpPost]
        public async Task<IActionResult> Create(ProductCreateDto dto)
        {
            var product = new Product
            {
                Code = dto.Code,
                Name = dto.Name,
                CostPrice = dto.CostPrice,
                RetailPrice = dto.RetailPrice,
                WholesalePrice = dto.WholesalePrice,
                Image = dto.Image,
                CategoryId = dto.CategoryId,
                IsActived = true,
                IsDeleted = false,
                CreationDate = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return Ok(product);
        }

        [Authorize(Roles = "administrador/a")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, ProductUpdateDto dto)
        {
            var product = await _context.Products.FindAsync(id)
                ?? throw new Exception("Producto no encontrado.");

            product.Code = dto.Code;
            product.Name = dto.Name;
            product.CostPrice = dto.CostPrice;
            product.RetailPrice = dto.RetailPrice;
            product.WholesalePrice = dto.WholesalePrice;
            product.Image = dto.Image;
            product.CategoryId = dto.CategoryId;

            await _context.SaveChangesAsync();

            return Ok(product);
        }
    }
}

