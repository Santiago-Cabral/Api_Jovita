using System.Collections.Generic;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.DTOs.Products;   // 👈 correcto

namespace ForrajeriaJovitaAPI.Services      // 👈 mismo namespace que StockService
{
    public interface IStockService
    {
        Task<List<ProductStockDto>> GetAllStockAsync(int? branchId = null, int? productId = null);
        Task<List<ProductStockDto>> GetStockByBranchAsync(int branchId);
        Task<List<ProductStockDto>> GetStockByProductAsync(int productId);
        Task<bool> UpdateStockAsync(UpdateStockDto dto);
        Task<bool> AddStockAsync(UpdateStockDto dto);
        Task<List<ProductStockDto>> GetLowStockAsync(decimal threshold = 10);
    }
}
