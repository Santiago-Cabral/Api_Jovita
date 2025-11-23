using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _service;

        public CategoriesController(ICategoryService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _service.GetAllAsync());

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
