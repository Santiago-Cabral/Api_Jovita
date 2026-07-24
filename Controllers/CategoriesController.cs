using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _service;
        private readonly ForrajeriaContext _context;

        public CategoriesController(ICategoryService service, ForrajeriaContext context)
        {
            _service = service;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _service.GetAllAsync());

        // GET: api/Categories/with-counts -> nombre + cantidad de productos (para el storefront)
        [HttpGet("with-counts")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllWithCounts()
        {
            var result = await _context.Products
                .Where(p => !p.IsDeleted && p.IsActived && p.CategoryId != null)
                .Include(p => p.Category)
                .GroupBy(p => p.Category.Name)
                .Select(g => new { title = g.Key, count = g.Count() })
                .ToListAsync();

            return Ok(result);
        }

        [Authorize(Roles = "administrador/a")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] string name) =>
            Ok(await _service.CreateAsync(name));

        [Authorize(Roles = "administrador/a")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] string name) =>
            Ok(await _service.UpdateAsync(id, name));

        [Authorize(Roles = "administrador/a")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id) =>
            Ok(await _service.DeleteAsync(id));
    }
}