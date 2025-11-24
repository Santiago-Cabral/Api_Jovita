/// ============================================
// StockService.cs - CORREGIDO
// ============================================
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.DTOs.Products;


namespace ForrajeriaJovitaAPI.Services
{
    public class StockService : IStockService
    {
        private readonly ForrajeriaContext _context;

        public StockService(ForrajeriaContext context)
        {
            _context = context;
        }

        public async Task<List<ProductStockDto>> GetAllStockAsync(int? branchId = null, int? productId = null)
        {
            var query = _context.ProductsStocks
                .Include(s => s.Product)
                .Include(s => s.Branch)
                .AsQueryable();

            if (branchId.HasValue)
                query = query.Where(s => s.BranchId == branchId.Value);

            if (productId.HasValue)
                query = query.Where(s => s.ProductId == productId.Value);

            var stocks = await query.ToListAsync();

            return stocks.Select(s => new ProductStockDto
            {
                BranchId = s.BranchId,
                BranchName = s.Branch.Name,
                Quantity = s.Quantity,
                LastUpdated = s.CreationDate
            }).ToList();
        }

        public async Task<List<ProductStockDto>> GetStockByBranchAsync(int branchId)
        {
            return await GetAllStockAsync(branchId, null);
        }

        public async Task<List<ProductStockDto>> GetStockByProductAsync(int productId)
        {
            return await GetAllStockAsync(null, productId);
        }

        public async Task<bool> UpdateStockAsync(UpdateStockDto dto)
        {
            var stock = await _context.ProductsStocks
                .FirstOrDefaultAsync(s => s.ProductId == dto.ProductId && s.BranchId == dto.BranchId);

            if (stock == null)
            {
                stock = new ProductStock
                {
                    ProductId = dto.ProductId,
                    BranchId = dto.BranchId,
                    Quantity = dto.Quantity,
                    CreationDate = DateTime.Now
                };
                _context.ProductsStocks.Add(stock);
            }
            else
            {
                stock.Quantity = dto.Quantity;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AddStockAsync(UpdateStockDto dto)
        {
            var stock = await _context.ProductsStocks
                .FirstOrDefaultAsync(s => s.ProductId == dto.ProductId && s.BranchId == dto.BranchId);

            if (stock == null)
            {
                stock = new ProductStock
                {
                    ProductId = dto.ProductId,
                    BranchId = dto.BranchId,
                    Quantity = dto.Quantity,
                    CreationDate = DateTime.Now
                };
                _context.ProductsStocks.Add(stock);
            }
            else
            {
                stock.Quantity += dto.Quantity;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ProductStockDto>> GetLowStockAsync(decimal threshold = 10)
        {
            var stocks = await _context.ProductsStocks
                .Where(s => s.Quantity <= threshold)
                .Include(s => s.Product)
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