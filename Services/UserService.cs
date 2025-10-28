// Services/UserService.cs
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs;
using ForrajeriaJovitaAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ForrajeriaJovitaAPI.Services
{
    public class UserService : IUserService
    {
        private readonly ForrajeriaContext _context;

        public UserService(ForrajeriaContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
        {
            return await _context.Users
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
        }

        public async Task<UserDto?> GetUserByIdAsync(int id)
        {
            return await _context.Users
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
        }

        public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.UserName == dto.UserName && !u.IsDeleted))
            {
                throw new InvalidOperationException("Ya existe un usuario con ese nombre de usuario");
            }

            var user = new User
            {
                Name = dto.Name,
                LastName = dto.LastName,
                UserName = dto.UserName,
                Password = dto.Password, // TODO: Hashear contraseña en producción
                RoleId = dto.RoleId,
                IsActived = true,
                IsDeleted = false,
                CreationDate = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                LastName = user.LastName,
                UserName = user.UserName,
                IsActived = user.IsActived
            };
        }

        public async Task<UserDto?> LoginAsync(LoginDto dto)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u =>
                    u.UserName == dto.UserName &&
                    u.Password == dto.Password && // TODO: Verificar hash en producción
                    !u.IsDeleted &&
                    u.IsActived);

            if (user == null) return null;

            return new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                LastName = user.LastName,
                UserName = user.UserName,
                IsActived = user.IsActived,
                RoleName = user.Role?.Name
            };
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
            if (user == null) return false;

            user.IsDeleted = true;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}