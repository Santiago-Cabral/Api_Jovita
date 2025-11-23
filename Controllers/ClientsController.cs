using ForrajeriaJovitaAPI.DTOs.Clients;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ForrajeriaJovitaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientsController : ControllerBase
    {
        private readonly IClientAccountService _service;

        public ClientsController(IClientAccountService service)
        {
            _service = service;
        }

        // =============================
        // CLIENTE WEB (perfil)
        // =============================

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMyClient()
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var client = await _service.GetMyClientAsync(userId);

            if (client == null)
                return NotFound(new { message = "El usuario no tiene cliente asignado." });

            return Ok(client);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateClientForUser(ClientCreateDto dto)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            return Ok(await _service.CreateForUserAsync(userId, dto));
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyClient(ClientUpdateDto dto)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            return Ok(await _service.UpdateMyClientAsync(userId, dto));
        }

        // =============================
        // ADMIN
        // =============================

        [Authorize(Roles = "administrador/a")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }

        [Authorize(Roles = "administrador/a")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var client = await _service.GetByIdAsync(id);
            if (client == null)
                return NotFound();

            return Ok(client);
        }

        [Authorize(Roles = "administrador/a")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateByAdmin(int id, ClientCreateDto dto)
        {
            return Ok(await _service.UpdateByAdminAsync(id, dto));
        }
    }
}
