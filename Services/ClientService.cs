// ============================================
// ClientService.cs - CORREGIDO
// ============================================
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.DTOs;

namespace ForrajeriaJovitaAPI.Services
{
    public class ClientService : IClientService
    {
        private readonly ForrajeriaContext _context;

        public ClientService(ForrajeriaContext context)
        {
            _context = context;
        }

        public async Task<List<ClientDto>> GetAllClientsAsync(string? search = null)
        {
            var query = _context.Clients.Where(c => !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => c.FullName.Contains(search) || c.Document.Contains(search));

            var clients = await query.ToListAsync();

            return clients.Select(c => new ClientDto
            {
                Id = c.Id,
                Name = c.FullName,
                Phone = c.Phone,
                DocumentNumber = c.Document,
                CreatedAt = c.CreationDate
            }).ToList();
        }

        public async Task<ClientDto?> GetClientByIdAsync(int id)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (client == null)
                return null;

            return new ClientDto
            {
                Id = client.Id,
                Name = client.FullName,
                Phone = client.Phone,
                DocumentNumber = client.Document,
                CreatedAt = client.CreationDate
            };
        }

        public async Task<ClientDto> CreateClientAsync(CreateClientDto dto)
        {
            // Validar documento único
            if (!string.IsNullOrWhiteSpace(dto.DocumentNumber) &&
                await _context.Clients.AnyAsync(c => c.Document == dto.DocumentNumber && !c.IsDeleted))
            {
                throw new InvalidOperationException("Ya existe un cliente con ese documento");
            }

            var client = new Client
            {
                FullName = dto.Name,
                Phone = dto.Phone ?? string.Empty,
                Document = dto.DocumentNumber ?? string.Empty,
                Amount = 0,
                DebitBalance = 0,
                IsDeleted = false,
                CreationDate = DateTime.Now
            };

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            return new ClientDto
            {
                Id = client.Id,
                Name = client.FullName,
                Phone = client.Phone,
                DocumentNumber = client.Document,
                CreatedAt = client.CreationDate
            };
        }

        public async Task<bool> DeleteClientAsync(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null || client.IsDeleted)
                return false;

            client.IsDeleted = true;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
