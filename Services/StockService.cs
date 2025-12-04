// Services/StockService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs.Products;
using ForrajeriaJovitaAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ForrajeriaJovitaAPI.Services
{
    public class StockService : IStockService
    {
        private readonly ForrajeriaContext _context;

        public StockService(ForrajeriaContext context)
        {
            _context = context;
        }

        // =====================================================
        // STOCK GENERAL (con filtros opcionales)
        // =====================================================
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

        // =====================================================
        // STOCK POR SUCURSAL
        // =====================================================
        public Task<List<ProductStockDto>> GetStockByBranchAsync(int branchId)
        {
            return GetAllStockAsync(branchId, null);
        }

        // =====================================================
        // STOCK POR PRODUCTO
        // =====================================================
        public Task<List<ProductStockDto>> GetStockByProductAsync(int productId)
        {
            return GetAllStockAsync(null, productId);
        }

        // =====================================================
        // SET STOCK (reemplaza cantidad)
        // =====================================================
        public async Task<bool> UpdateStockAsync(UpdateStockDto dto)
        {
            var stock = await _context.ProductsStocks
                .FirstOrDefaultAsync(s =>
                    s.ProductId == dto.ProductId &&
                    s.BranchId == dto.BranchId);

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

        // =====================================================
        // ADD STOCK (suma cantidad)
        // =====================================================
        public async Task<bool> AddStockAsync(UpdateStockDto dto)
        {
            var stock = await _context.ProductsStocks
                .FirstOrDefaultAsync(s =>
                    s.ProductId == dto.ProductId &&
                    s.BranchId == dto.BranchId);

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

        // =====================================================
        // STOCK BAJO
        // =====================================================
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

