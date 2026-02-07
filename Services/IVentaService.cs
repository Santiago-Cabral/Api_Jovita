using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.DTOs;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IVentaService
    {
        Task<IEnumerable<SaleDto>> GetAllSalesAsync(DateTime? startDate = null, DateTime? endDate = null, int? sellerId = null);
        Task<SaleDto?> GetSaleByIdAsync(int id);
        Task<SaleDto> CreatePublicSaleAsync(CreatePublicSaleDto dto);
        Task<SaleDto> CreateSaleAsync(CreateSaleDto dto);
        Task<SaleDto?> UpdateSaleAsync(int id, UpdateSaleDto dto);
        Task<object> GetTodaySalesSummaryAsync();
        Task<bool> DeleteSaleAsync(int id);
    }
}