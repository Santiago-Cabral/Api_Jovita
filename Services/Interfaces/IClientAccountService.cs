using ForrajeriaJovitaAPI.DTOs.Clients;

namespace ForrajeriaJovitaAPI.Services.Interfaces
{
    public interface IClientAccountService
    {
        Task<ClientResponseDto?> GetMyClientAsync(int userId);
        Task<ClientResponseDto> CreateForUserAsync(int userId, ClientCreateDto dto);
        Task<ClientResponseDto> UpdateMyClientAsync(int userId, ClientUpdateDto dto);

        // Admin
        Task<IEnumerable<ClientResponseDto>> GetAllAsync();
        Task<ClientResponseDto?> GetByIdAsync(int id);
        Task<ClientResponseDto> UpdateByAdminAsync(int id, ClientCreateDto dto);
    }
}
