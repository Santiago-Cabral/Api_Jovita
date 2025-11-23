using ForrajeriaJovitaAPI.Models;

namespace ForrajeriaJovitaAPI.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<IEnumerable<Category>> GetAllAsync();
        Task<Category> CreateAsync(string name);
        Task<Category> UpdateAsync(int id, string name);
        Task<bool> DeleteAsync(int id);
    }
}
