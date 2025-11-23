using ForrajeriaJovitaAPI.DTOs.Products;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IProductoService
    {
        Task<IEnumerable<ProductResponseDto>> GetAllAsync();
        Task<ProductResponseDto?> GetByIdAsync(int id);
        Task<ProductResponseDto> CreateAsync(ProductCreateDto dto);
        Task<ProductResponseDto?> UpdateAsync(int id, ProductUpdateDto dto);
        Task<bool> DeleteAsync(int id);

        Task<IEnumerable<ProductStockDto>> GetStocksAsync(int productId);
    }
}
