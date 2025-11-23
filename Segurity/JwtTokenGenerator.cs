using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ForrajeriaJovitaAPI.Models;
using Microsoft.IdentityModel.Tokens;

namespace ForrajeriaJovitaAPI.Security
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(User user, string roleName);
    }

    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly JwtSettings _settings;

        public JwtTokenGenerator(JwtSettings settings)
        {
            _settings = settings;
        }

        public string GenerateToken(User user, string roleName)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.UserName),
                new Claim(ClaimTypes.Role, roleName),
                new Claim(ClaimTypes.Name, $"{user.Name} {user.LastName}")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_settings.ExpiresMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
