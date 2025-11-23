using ForrajeriaJovitaAPI.DTOs.Products;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IProductoService
    {
        Task<List<ProductResponseDto>> GetAllProductsAsync(bool? isActived = null, string? search = null);

        Task<ProductResponseDto?> GetProductByIdAsync(int id);

        Task<ProductResponseDto> CreateProductAsync(ProductCreateDto dto);

        Task<bool> UpdateProductAsync(int id, ProductUpdateDto dto);

        Task<bool> DeleteProductAsync(int id);

        Task<List<ProductStockDto>> GetProductStockAsync(int productId);
    }
}
