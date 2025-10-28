using ForrajeriaJovitaAPI.DTOs;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IClientService
    {
        Task<List<ClientDto>> GetAllClientsAsync(string? search = null);
        Task<ClientDto?> GetClientByIdAsync(int id);
        Task<ClientDto> CreateClientAsync(CreateClientDto dto);
        Task<bool> DeleteClientAsync(int id);
    }
}