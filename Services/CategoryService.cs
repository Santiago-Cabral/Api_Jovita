using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ForrajeriaJovitaAPI.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ForrajeriaContext _context;

        public CategoryService(ForrajeriaContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            return await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        }

        public async Task<Category> CreateAsync(string name)
        {
            var exists = await _context.Categories.AnyAsync(c => c.Name == name);
            if (exists) throw new Exception("La categoría ya existe.");

            var category = new Category { Name = name };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return category;
        }

        public async Task<Category> UpdateAsync(int id, string name)
        {
            var category = await _context.Categories.FindAsync(id)
                ?? throw new Exception("Categoría no encontrada.");

            category.Name = name;
            await _context.SaveChangesAsync();
            return category;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return false;

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}

