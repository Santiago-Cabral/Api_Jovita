// Services/IVentaService.cs
using ForrajeriaJovitaAPI.DTOs;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IVentaService
    {
        Task<IEnumerable<SaleDto>> GetAllSalesAsync(DateTime? startDate = null, DateTime? endDate = null, int? sellerId = null);
        Task<SaleDto?> GetSaleByIdAsync(int id);
        Task<SaleDto> CreateSaleAsync(CreateSaleDto dto);
        Task<object> GetTodaySalesSummaryAsync();

        // 👉 NUEVO
        Task<SaleDto?> UpdateSaleAsync(UpdateSaleDto dto);
    }

}