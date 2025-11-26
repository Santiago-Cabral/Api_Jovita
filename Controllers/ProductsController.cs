using ForrajeriaJovitaAPI.DTOs.Products;
using ForrajeriaJovitaAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductoService _service;

        public ProductsController(IProductoService service)
        {
            _service = service;
        }

        // ===========================
        // CLIENTE - CATÁLOGO (PÚBLICO)
        // ===========================
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound("Producto no encontrado.");

            return Ok(result);
        }

        // ===========================
        // STOCK POR SUCURSAL (PÚBLICO)
        // ===========================
        [AllowAnonymous]
        [HttpGet("{id}/stock")]
        public async Task<IActionResult> GetStock(int id)
        {
            var result = await _service.GetStocksAsync(id);
            return Ok(result);
        }

        // ===========================
        // ADMIN
        // ===========================
        [Authorize(Roles = "administrador/a")]
        [HttpPost]
        public async Task<IActionResult> Create(ProductCreateDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [Authorize(Roles = "administrador/a")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, ProductUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            if (updated == null)
                return NotFound("Producto no encontrado.");

            return Ok(updated);
        }

        [Authorize(Roles = "administrador/a")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound("Producto no encontrado.");

            return NoContent();
        }
    }
}

