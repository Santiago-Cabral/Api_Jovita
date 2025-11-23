using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs.Auth;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Security;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ForrajeriaJovitaAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly ForrajeriaContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenGenerator _jwt;

        private const int ROLE_CLIENTE = 3;

        public AuthService(
            ForrajeriaContext context,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator jwt)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _jwt = jwt;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            var exists = await _context.Users
                .AnyAsync(u => u.UserName == dto.Email && !u.IsDeleted);

            if (exists)
                throw new Exception("El email ya está registrado.");

            var user = new User
            {
                Name = dto.Name,
                LastName = dto.LastName,
                UserName = dto.Email,
                Password = _passwordHasher.Hash(dto.Password),
                CreationDate = DateTime.UtcNow,
                IsActived = true,
                IsDeleted = false,
                RoleId = ROLE_CLIENTE
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _jwt.GenerateToken(user, "cliente");

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Role = "cliente",
                FullName = $"{user.Name} {user.LastName}",
                Email = user.UserName
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserName == dto.Email && !u.IsDeleted && u.IsActived);

            if (user == null)
                throw new Exception("Credenciales inválidas.");

            if (!_passwordHasher.Verify(dto.Password, user.Password))
                throw new Exception("Credenciales inválidas.");

            var roleName = user.Role?.Name ?? "cliente";

            var token = _jwt.GenerateToken(user, roleName);

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Role = roleName,
                FullName = $"{user.Name} {user.LastName}",
                Email = user.UserName
            };
        }
    }
}
