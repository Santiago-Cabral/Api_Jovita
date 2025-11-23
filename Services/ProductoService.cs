using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.DTOs.Products;

namespace ForrajeriaJovitaAPI.Services
{
    public class ProductoService : IProductoService
    {
        private readonly ForrajeriaContext _context;

        public ProductoService(ForrajeriaContext context)
        {
            _context = context;
        }

        public async Task<List<ProductResponseDto>> GetAllProductsAsync(bool? isActived = null, string? search = null)
        {
            var query = _context.Products.Where(p => !p.IsDeleted);

            if (isActived.HasValue)
                query = query.Where(p => p.IsActived == isActived.Value);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Name.Contains(search) || p.Code.Contains(search));

            var products = await query.ToListAsync();

            return products.Select(p => new ProductResponseDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                CostPrice = p.CostPrice,
                RetailPrice = p.RetailPrice,
                WholesalePrice = p.WholesalePrice,
                IsActived = p.IsActived,
                BaseUnit = p.BaseUnit.ToString(),
                UpdateDate = p.UpdateDate
            }).ToList();
        }

        public async Task<ProductResponseDto?> GetProductByIdAsync(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (product == null)
                return null;

            return new ProductResponseDto
            {
                Id = product.Id,
                Code = product.Code,
                Name = product.Name,
                CostPrice = product.CostPrice,
                RetailPrice = product.RetailPrice,
                WholesalePrice = product.WholesalePrice,
                IsActived = product.IsActived,
                BaseUnit = product.BaseUnit.ToString(),
                UpdateDate = product.UpdateDate
            };
        }

        public async Task<ProductResponseDto> CreateProductAsync(ProductCreateDto dto)
        {
            if (await _context.Products.AnyAsync(p => p.Code == dto.Code && !p.IsDeleted))
                throw new InvalidOperationException("Ya existe un producto con ese código");

            var product = new Product
            {
                Code = dto.Code,
                Name = dto.Name,
                CostPrice = dto.CostPrice,
                RetailPrice = dto.RetailPrice,
                WholesalePrice = dto.WholesalePrice,
                BaseUnit = (BaseUnit)dto.BaseUnit,
                IsActived = true,
                IsDeleted = false,
                CreationDate = DateTime.Now
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return new ProductResponseDto
            {
                Id = product.Id,
                Code = product.Code,
                Name = product.Name,
                CostPrice = product.CostPrice,
                RetailPrice = product.RetailPrice,
                WholesalePrice = product.WholesalePrice,
                IsActived = product.IsActived,
                BaseUnit = product.BaseUnit.ToString()
            };
        }

        public async Task<bool> UpdateProductAsync(int id, ProductUpdateDto dto)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null || product.IsDeleted)
                return false;

            if (dto.Name != null) product.Name = dto.Name;
            if (dto.CostPrice.HasValue) product.CostPrice = dto.CostPrice.Value;
            if (dto.RetailPrice.HasValue) product.RetailPrice = dto.RetailPrice.Value;
            if (dto.WholesalePrice.HasValue) product.WholesalePrice = dto.WholesalePrice.Value;
            if (dto.IsActived.HasValue) product.IsActived = dto.IsActived.Value;

            product.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null || product.IsDeleted)
                return false;

            product.IsDeleted = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ProductStockDto>> GetProductStockAsync(int productId)
        {
            var stocks = await _context.ProductsStocks
                .Where(s => s.ProductId == productId)
                .Include(s => s.Branch)
                .ToListAsync();

            return stocks.Select(s => new ProductStockDto
            {
                BranchId = s.BranchId,
                BranchName = s.Branch.Name,
                Quantity = s.Quantity,
                LastUpdated = s.CreationDate
            }).ToList();
        }
    }
}
