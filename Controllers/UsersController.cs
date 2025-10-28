// Controllers/UsersController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs;
using ForrajeriaJovitaAPI.Models;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly ForrajeriaContext _context;

        public UsersController(ForrajeriaContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
        {
            var users = await _context.Users
                .Where(u => !u.IsDeleted)
                .Include(u => u.Role)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    LastName = u.LastName,
                    UserName = u.UserName,
                    IsActived = u.IsActived,
                    RoleName = u.Role != null ? u.Role.Name : null
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id && !u.IsDeleted)
                .Include(u => u.Role)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    LastName = u.LastName,
                    UserName = u.UserName,
                    IsActived = u.IsActived,
                    RoleName = u.Role != null ? u.Role.Name : null
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            return Ok(user);
        }

        [HttpPost]
        public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.UserName == dto.UserName && !u.IsDeleted))
            {
                return BadRequest(new { message = "Ya existe un usuario con ese nombre de usuario" });
            }

            // En producción, deberías hashear la contraseña
            var user = new User
            {
                Name = dto.Name,
                LastName = dto.LastName,
                UserName = dto.UserName,
                Password = dto.Password, // TODO: Hashear contraseña
                RoleId = dto.RoleId,
                IsActived = true,
                IsDeleted = false,
                CreationDate = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var userDto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                LastName = user.LastName,
                UserName = user.UserName,
                IsActived = user.IsActived
            };

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, userDto);
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto dto)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u =>
                    u.UserName == dto.UserName &&
                    u.Password == dto.Password && // TODO: Verificar hash
                    !u.IsDeleted &&
                    u.IsActived);

            if (user == null)
            {
                return Unauthorized(new { message = "Credenciales incorrectas" });
            }

            var userDto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                LastName = user.LastName,
                UserName = user.UserName,
                IsActived = user.IsActived,
                RoleName = user.Role?.Name
            };

            return Ok(userDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            user.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Usuario eliminado correctamente" });
        }
    }
}