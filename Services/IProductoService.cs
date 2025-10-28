// ============================================
// IProductoService.cs - CORREGIDO
// ============================================
using ForrajeriaJovitaAPI.DTOs;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IProductoService
    {
        Task<List<ProductDto>> GetAllProductsAsync(bool? isActived = null, string? search = null);
        Task<ProductDto?> GetProductByIdAsync(int id);
        Task<ProductDto> CreateProductAsync(CreateProductDto dto);
        Task<bool> UpdateProductAsync(int id, UpdateProductDto dto);
        Task<bool> DeleteProductAsync(int id);
        Task<List<ProductStockDto>> GetProductStockAsync(int productId);
    }
}