using ForrajeriaJovitaAPI.DTOs;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForrajeriaJovitaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest(new { message = "Los datos son requeridos" });

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _auth.RegisterAsync(dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException uaEx)
            {
                return Unauthorized(new { message = uaEx.Message });
            }
            catch (Exception ex)
            {
                // Loggear aquí si querés más info: _logger.LogError(ex, "Error en Register");
                return StatusCode(500, new { message = "Error interno del servidor", detail = ex.Message });
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest(new { message = "Los datos son requeridos" });

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _auth.LoginAsync(dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException uaEx)
            {
                return Unauthorized(new { message = uaEx.Message });
            }
            catch (Exception ex)
            {
                // Loggear aquí si querés más info: _logger.LogError(ex, "Error en Login");
                return StatusCode(500, new { message = "Error interno del servidor", detail = ex.Message });
            }
        }
    }
}
