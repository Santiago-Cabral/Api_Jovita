using ForrajeriaJovitaAPI.DTOs;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IVentaService
    {
        Task<IEnumerable<SaleDto>> GetAllSalesAsync(DateTime? start, DateTime? end, int? sellerId);
        Task<SaleDto?> GetSaleByIdAsync(int id);
        Task<SaleDto> CreateSaleAsync(CreateSaleDto dto);
        Task<SaleDto?> UpdateSaleAsync(int id, UpdateSaleDto dto);
        Task<object> GetTodaySalesSummaryAsync();
        Task<SaleDto> CreatePublicSaleAsync(CreatePublicSaleDto dto);

    }
}
