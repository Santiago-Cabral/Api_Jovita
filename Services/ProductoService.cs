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

        // =====================================
        // GET ALL (ASYNC)
        // =====================================
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
                RetailPrice = p.RetailPrice,
                WholesalePrice = p.WholesalePrice,
                BaseUnit = (int)p.BaseUnit,
                IsActived = p.IsActived,
                UpdateDate = p.UpdateDate ?? DateTime.MinValue,

                Image = p.Image,
                CategoryId = p.CategoryId ?? 0,
                CategoryName = p.Category?.Name,

                Stock = p.ProductsStocks.Sum(s => (int)s.Quantity)
            });
        }

        // =====================================
        // GET BY ID (ASYNC)
        // =====================================
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
                RetailPrice = product.RetailPrice,
                WholesalePrice = product.WholesalePrice,
                BaseUnit = (int)product.BaseUnit,
                IsActived = product.IsActived,
                UpdateDate = product.UpdateDate ?? DateTime.MinValue,

                Image = product.Image,
                CategoryId = product.CategoryId ?? 0,
                CategoryName = product.Category?.Name,

                Stock = product.ProductsStocks.Sum(s => (int)s.Quantity)
            };
        }

        // =====================================
        // CREATE (ASYNC)
        // =====================================
        public async Task<ProductResponseDto> CreateAsync(ProductCreateDto dto)
        {
            if (await _context.Products.AnyAsync(p => p.Code == dto.Code && !p.IsDeleted))
                throw new Exception("Ya existe un producto con ese código.");

            var product = new Product
            {
                Code = dto.Code,
                Name = dto.Name,
                CostPrice = dto.CostPrice,
                RetailPrice = dto.RetailPrice,
                WholesalePrice = dto.WholesalePrice,
                BaseUnit = (BaseUnit)dto.BaseUnit,
                Image = dto.Image,
                CategoryId = dto.CategoryId,

                IsActived = dto.IsActived,
                IsDeleted = false,
                CreationDate = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(product.Id)
                   ?? throw new Exception("Error al crear producto");
        }

        // =====================================
        // UPDATE (ASYNC)
        // =====================================
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

            product.Image = dto.Image;
            product.CategoryId = dto.CategoryId;

            product.IsActived = dto.IsActived;
            product.UpdateDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await GetByIdAsync(id);
        }

        // =====================================
        // DELETE (ASYNC)
        // =====================================
        public async Task<bool> DeleteAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null || product.IsDeleted)
                return false;

            product.IsDeleted = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // =====================================
        // GET STOCKS (ASYNC)
        // =====================================
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
