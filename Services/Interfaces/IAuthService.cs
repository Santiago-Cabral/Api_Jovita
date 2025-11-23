using ForrajeriaJovitaAPI.DTOs.Auth;

namespace ForrajeriaJovitaAPI.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
    }
}
