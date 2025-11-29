using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs;
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

        public AuthService(
            ForrajeriaContext context,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator jwt)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _jwt = jwt;
        }

        // 🔥 Convertimos RoleId → Rol que usa Authorize
        private string MapRole(int? roleId)
        {
            return roleId switch
            {
                1 => "administrador/a",
                2 => "empleado",
                3 => "cliente",
                _ => "cliente"
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == dto.Email &&
                    !u.IsDeleted &&
                    u.IsActived);

            if (user == null)
                throw new Exception("Credenciales inválidas.");

            if (!_passwordHasher.Verify(dto.Password, user.Password))
                throw new Exception("Credenciales inválidas.");

            var roleName = MapRole(user.RoleId);

            var token = _jwt.GenerateToken(user, roleName);

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Role = roleName,
                FullName = $"{user.Name} {user.LastName}",
                Email = user.Email
            };
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
                RoleId = 3 // cliente
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
    }
}

