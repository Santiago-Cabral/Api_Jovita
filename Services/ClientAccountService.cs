using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs.Clients;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ForrajeriaJovitaAPI.Services
{
    public class ClientAccountService : IClientAccountService
    {
        private readonly ForrajeriaContext _context;

        public ClientAccountService(ForrajeriaContext context)
        {
            _context = context;
        }

        // =============================
        // CLIENTE WEB
        // =============================

        public async Task<ClientResponseDto?> GetMyClientAsync(int userId)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserId == userId && !c.IsDeleted);

            if (client == null)
                return null;

            return new ClientResponseDto
            {
                Id = client.Id,
                FullName = client.FullName,
                Phone = client.Phone,
                Document = client.Document,
                Amount = client.Amount,
                DebitBalance = client.DebitBalance
            };
        }

        public async Task<ClientResponseDto> CreateForUserAsync(int userId, ClientCreateDto dto)
        {
            // evitar duplicados
            var existsDni = await _context.Clients
                .AnyAsync(c => c.Document == dto.Document && !c.IsDeleted);

            if (existsDni)
                throw new Exception("Ya existe un cliente con ese DNI.");

            var existsByUser = await _context.Clients
                .AnyAsync(c => c.UserId == userId);

            if (existsByUser)
                throw new Exception("Este usuario ya tiene un cliente asignado.");

            var client = new Client
            {
                FullName = dto.FullName,
                Phone = dto.Phone,
                Document = dto.Document,
                Amount = 0,
                DebitBalance = 0,
                UserId = userId,
                CreationDate = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            return new ClientResponseDto
            {
                Id = client.Id,
                FullName = client.FullName,
                Phone = client.Phone,
                Document = client.Document,
                Amount = client.Amount,
                DebitBalance = client.DebitBalance
            };
        }

        public async Task<ClientResponseDto> UpdateMyClientAsync(int userId, ClientUpdateDto dto)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserId == userId && !c.IsDeleted);

            if (client == null)
                throw new Exception("El cliente no existe.");

            client.FullName = dto.FullName;
            client.Phone = dto.Phone;

            await _context.SaveChangesAsync();

            return new ClientResponseDto
            {
                Id = client.Id,
                FullName = client.FullName,
                Phone = client.Phone,
                Document = client.Document,
                Amount = client.Amount,
                DebitBalance = client.DebitBalance
            };
        }

        // =============================
        // ADMIN
        // =============================

        public async Task<IEnumerable<ClientResponseDto>> GetAllAsync()
        {
            return await _context.Clients
                .Where(c => !c.IsDeleted)
                .Select(c => new ClientResponseDto
                {
                    Id = c.Id,
                    FullName = c.FullName,
                    Phone = c.Phone,
                    Document = c.Document,
                    Amount = c.Amount,
                    DebitBalance = c.DebitBalance
                })
                .ToListAsync();
        }

        public async Task<ClientResponseDto?> GetByIdAsync(int id)
        {
            var c = await _context.Clients.FindAsync(id);

            if (c == null || c.IsDeleted)
                return null;

            return new ClientResponseDto
            {
                Id = c.Id,
                FullName = c.FullName,
                Phone = c.Phone,
                Document = c.Document,
                Amount = c.Amount,
                DebitBalance = c.DebitBalance
            };
        }

        public async Task<ClientResponseDto> UpdateByAdminAsync(int id, ClientCreateDto dto)
        {
            var client = await _context.Clients.FindAsync(id);

            if (client == null || client.IsDeleted)
                throw new Exception("El cliente no existe.");

            // validar DNI duplicado
            var existsDni = await _context.Clients
                .AnyAsync(c => c.Document == dto.Document && c.Id != id);

            if (existsDni)
                throw new Exception("Ya existe otro cliente con ese DNI.");

            client.FullName = dto.FullName;
            client.Phone = dto.Phone;
            client.Document = dto.Document;

            await _context.SaveChangesAsync();

            return new ClientResponseDto
            {
                Id = client.Id,
                FullName = client.FullName,
                Phone = client.Phone,
                Document = client.Document,
                Amount = client.Amount,
                DebitBalance = client.DebitBalance
            };
        }
    }
}
