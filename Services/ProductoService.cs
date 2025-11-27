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

        public async Task<IEnumerable<ProductResponseDto>> GetAllAsync()
        {
            var products = await _context.Products
                .Where(p => !p.IsDeleted)
                .Include(p => p.Category)
                .Include(p => p.ProductsStocks)
                .ToListAsync();

            return products.Select(p => new ProductResponseDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                CostPrice = p.CostPrice,
                RetailPrice = p.RetailPrice,
                WholesalePrice = p.WholesalePrice,
                BaseUnit = (int)p.BaseUnit,
                UpdateDate = p.UpdateDate ?? DateTime.MinValue,
                IsActived = p.IsActived,
                CategoryId = p.CategoryId ?? 0,
                CategoryName = p.Category?.Name,
                Image = p.Image,

                // STOCK DECIMAL ✔
                Stock = p.ProductsStocks.Sum(s => s.Quantity)
            });
        }

        public async Task<ProductResponseDto?> GetByIdAsync(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductsStocks)
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
                BaseUnit = (int)product.BaseUnit,
                UpdateDate = product.UpdateDate ?? DateTime.MinValue,
                IsActived = product.IsActived,
                CategoryId = product.CategoryId ?? 0,
                CategoryName = product.Category?.Name,
                Image = product.Image,

                // STOCK DECIMAL ✔
                Stock = product.ProductsStocks.Sum(s => s.Quantity)
            };
        }

        public async Task<ProductResponseDto> CreateAsync(ProductCreateDto dto)
        {
            var product = new Product
            {
                Code = dto.Code,
                Name = dto.Name,
                CostPrice = dto.CostPrice,
                RetailPrice = dto.RetailPrice,
                WholesalePrice = dto.WholesalePrice,
                BaseUnit = (BaseUnit)dto.BaseUnit,
                CategoryId = dto.CategoryId,
                Image = dto.Image,
                IsActived = dto.IsActived,
                IsDeleted = false,
                CreationDate = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(product.Id);
        }

        public async Task<ProductResponseDto?> UpdateAsync(int id, ProductUpdateDto dto)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null || product.IsDeleted)
                return null;

            product.Code = dto.Code;
            product.Name = dto.Name;
            product.CostPrice = dto.CostPrice;
            product.RetailPrice = dto.RetailPrice;
            product.WholesalePrice = dto.WholesalePrice;
            product.BaseUnit = (BaseUnit)dto.BaseUnit;
            product.CategoryId = dto.CategoryId;
            product.Image = dto.Image;
            product.IsActived = dto.IsActived;
            product.UpdateDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await GetByIdAsync(id);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null || product.IsDeleted)
                return false;

            product.IsDeleted = true;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<IEnumerable<ProductStockDto>> GetStocksAsync(int productId)
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
            });
        }
    }
}
