// ============================================
// BranchService.cs - CORREGIDO
// ============================================
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.DTOs;

namespace ForrajeriaJovitaAPI.Services
{
    public class BranchService : IBranchService
    {
        private readonly ForrajeriaContext _context;

        public BranchService(ForrajeriaContext context)
        {
            _context = context;
        }

        public async Task<List<BranchDto>> GetAllBranchesAsync(bool? isActived = null)
        {
            var query = _context.Branches.Where(b => !b.IsDeleted);

            if (isActived.HasValue)
                query = query.Where(b => b.IsActived == isActived.Value);

            var branches = await query.ToListAsync();

            return branches.Select(b => new BranchDto
            {
                Id = b.Id,
                Name = b.Name,
                Address = b.Address,
                IsActive = b.IsActived,
                CreatedAt = b.CreationDate
            }).ToList();
        }

        public async Task<BranchDto?> GetBranchByIdAsync(int id)
        {
            var branch = await _context.Branches
                .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

            if (branch == null)
                return null;

            return new BranchDto
            {
                Id = branch.Id,
                Name = branch.Name,
                Address = branch.Address,
                IsActive = branch.IsActived,
                CreatedAt = branch.CreationDate
            };
        }

        public async Task<BranchDto> CreateBranchAsync(CreateBranchDto dto)
        {
            var branch = new Branch
            {
                Name = dto.Name,
                Address = dto.Address ?? string.Empty,
                IsActived = dto.IsActive,
                IsDeleted = false,
                CreationDate = DateTime.Now
            };

            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();

            return new BranchDto
            {
                Id = branch.Id,
                Name = branch.Name,
                Address = branch.Address,
                IsActive = branch.IsActived,
                CreatedAt = branch.CreationDate
            };
        }

        public async Task<bool> DeleteBranchAsync(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null || branch.IsDeleted)
                return false;

            branch.IsDeleted = true;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
