using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs;
using ForrajeriaJovitaAPI.Models;

namespace ForrajeriaJovitaAPI.Services
{
    public class VentaService : IVentaService
    {
        private readonly ForrajeriaContext _context;
        private const int CASA_CENTRAL_BRANCH_ID = 1;

        public VentaService(ForrajeriaContext context)
        {
            _context = context;
        }

        // =========================================================
        // GET ALL SALES
        // =========================================================
        public async Task<IEnumerable<SaleDto>> GetAllSalesAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? sellerId = null)
        {
            // Filtrar ventas eliminadas (soft delete)
            var query = _context.Sales
                .Where(s => !s.IsDeleted)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(s => s.SoldAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.SoldAt <= endDate.Value);

            if (sellerId.HasValue)
                query = query.Where(s => s.SellerUserId == sellerId.Value);

            var sales = await query
                .Include(s => s.Client) // <-- incluir cliente para obtener nombre/datos
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems).ThenInclude(i => i.Product)
                .Include(s => s.SalesPayments)
                .OrderByDescending(s => s.SoldAt)
                .ToListAsync();

            return sales.Select(MapSaleToDto);
        }

        // =========================================================
        // GET SALE BY ID
        // =========================================================
        public async Task<SaleDto?> GetSaleByIdAsync(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.Client)
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems).ThenInclude(i => i.Product)
                .Include(s => s.SalesPayments)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            return sale == null ? null : MapSaleToDto(sale);
        }

        // =========================================================
        // CREATE PUBLIC SALE (WEB) + STOCK
        // =========================================================
        public async Task<SaleDto> CreatePublicSaleAsync(CreatePublicSaleDto dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Descontar stock Casa Central
                foreach (var item in dto.Items)
                {
                    var stock = await _context.ProductsStocks.FirstOrDefaultAsync(s =>
                        s.ProductId == item.ProductId &&
                        s.BranchId == CASA_CENTRAL_BRANCH_ID);

                    if (stock == null || stock.Quantity < item.Quantity)
                        throw new InvalidOperationException("Stock insuficiente");

                    stock.Quantity -= item.Quantity;
                }

                // 2. Datos base
                var user = await _context.Users.FirstAsync();
                var cashSession = await _context.CashSessions
                    .OrderByDescending(c => c.Id)
                    .FirstAsync();

                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal total = subtotal + dto.ShippingCost;

                // 3. Cash movement
                var cashMovement = new CashMovement
                {
                    CashSessionId = cashSession.Id,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    CreationDate = DateTime.Now
                };

                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                // 4. Sale
                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SellerUserId = user.Id,
                    SoldAt = DateTime.Now,
                    Subtotal = subtotal,
                    DiscountTotal = 0,
                    Total = total,
                    PaymentStatus = 1,
                    CreationDate = DateTime.Now
                };

                // Si el DTO trae DeliveryAddress / ClientId opcional, asignalo (para compatibilidad)
                if (!string.IsNullOrWhiteSpace(dto.DeliveryAddress))
                    sale.DeliveryAddress = dto.DeliveryAddress;

                if (dto.ClientId.HasValue)
                    sale.ClientId = dto.ClientId.Value;

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // 5. Items
                foreach (var item in dto.Items)
                {
                    _context.SalesItems.Add(new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Discount = 0,
                        CreationDate = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return (await GetSaleByIdAsync(sale.Id))!;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // =========================================================
        // CREATE INTERNAL SALE (CAJA)
        // =========================================================
        public async Task<SaleDto> CreateSaleAsync(CreateSaleDto dto)
        {
            throw new NotImplementedException("Venta interna aún no implementada");
        }

        // =========================================================
        // UPDATE SALE
        // =========================================================
        public async Task<SaleDto?> UpdateSaleAsync(int id, UpdateSaleDto dto)
        {
            var sale = await _context.Sales.FindAsync(id);
            if (sale == null) return null;

            if (dto.PaymentStatus.HasValue)
                sale.PaymentStatus = dto.PaymentStatus.Value;

            if (dto.DeliveryCost.HasValue)
                sale.DeliveryCost = dto.DeliveryCost.Value;

            if (!string.IsNullOrWhiteSpace(dto.DeliveryAddress))
                sale.DeliveryAddress = dto.DeliveryAddress;

            // si el DTO trae clientId o clientName podrías asociar aquí (opcional)
            if (dto.ClientId.HasValue)
                sale.ClientId = dto.ClientId.Value;

            await _context.SaveChangesAsync();
            return await GetSaleByIdAsync(id);
        }

        // =========================================================
        // TODAY SUMMARY
        // =========================================================
        public async Task<object> GetTodaySalesSummaryAsync()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var sales = await _context.Sales
                .Where(s => s.SoldAt >= today && s.SoldAt < tomorrow && !s.IsDeleted)
                .ToListAsync();

            return new
            {
                Date = today,
                TotalSales = sales.Count,
                TotalAmount = sales.Sum(s => s.Total)
            };
        }

        public async Task<bool> DeleteSaleAsync(int id)
        {
            var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return false;

            sale.IsDeleted = true;

            await _context.SaveChangesAsync();
            return true;
        }

        // =========================================================
        // MAP
        // =========================================================
        private static SaleDto MapSaleToDto(Sale s)
        {
            // Mapear items
            var items = s.SalesItems?.Select(i => new SaleItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? i.ProductName ?? "Producto",
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                Discount = i.Discount,
                Total = (i.UnitPrice * i.Quantity) - i.Discount
            }).ToList() ?? new List<SaleItemDto>();

            // Mapear payments
            var payments = s.SalesPayments?.Select(p => new SalePaymentDto
            {
                MethodName = p.MethodName ?? p.Method ?? string.Empty,
                Amount = p.Amount,
                Reference = p.Reference
            }).ToList() ?? new List<SalePaymentDto>();

            return new SaleDto
            {
                Id = s.Id,
                SoldAt = s.SoldAt,
                SellerName = s.SellerUser != null
                    ? (s.SellerUser.FullName ?? s.SellerUser.UserName ?? "E-commerce")
                    : "E-commerce",

                Subtotal = s.Subtotal,
                DiscountTotal = s.DiscountTotal,
                Total = s.Total,

                ClientId = s.ClientId,
                ClientName = s.Client != null ? s.Client.FullName : null,

                DeliveryType = s.DeliveryType,
                DeliveryAddress = s.DeliveryAddress,
                DeliveryCost = s.DeliveryCost,
                DeliveryNote = s.DeliveryNote,

                PaymentStatus = s.PaymentStatus,
                Items = items,
                Payments = payments
            };
        }
    }
}


