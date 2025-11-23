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
        // GET ALL
        // =====================================
        public async Task<IEnumerable<ProductResponseDto>> GetAllAsync()
        {
            var products = await _context.Products
                .Where(p => !p.IsDeleted)
                .ToListAsync();

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
            });
        }

        // =====================================
        // GET BY ID
        // =====================================
        public async Task<ProductResponseDto?> GetByIdAsync(int id)
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

        // =====================================
        // CREATE
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
                Image = dto.Image,
                CategoryId = dto.CategoryId,
                BaseUnit = (BaseUnit)dto.BaseUnit,
                IsActived = true,
                IsDeleted = false,
                CreationDate = DateTime.UtcNow
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
                BaseUnit = product.BaseUnit.ToString(),
                UpdateDate = product.UpdateDate
            };
        }

        // =====================================
        // UPDATE
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
            product.Image = dto.Image;
            product.CategoryId = dto.CategoryId;
            product.UpdateDate = DateTime.UtcNow;

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
                BaseUnit = product.BaseUnit.ToString(),
                UpdateDate = product.UpdateDate
            };
        }

        // =====================================
        // DELETE
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
        // GET STOCKS
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

