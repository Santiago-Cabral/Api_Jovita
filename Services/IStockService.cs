using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.DTOs.Products;
using ForrajeriaJovitaAPI.Services.Interfaces; // 👈 IMPORTANTE

namespace ForrajeriaJovitaAPI.Services.Interfaces
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
