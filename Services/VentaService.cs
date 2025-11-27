using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs;
using ForrajeriaJovitaAPI.Models;

namespace ForrajeriaJovitaAPI.Services
{
    public class VentaService : IVentaService
    {
        private readonly ForrajeriaContext _context;

        public VentaService(ForrajeriaContext context)
        {
            _context = context;
        }

        // ==========================================================
        // GET ALL SALES
        // ==========================================================
        public async Task<IEnumerable<SaleDto>> GetAllSalesAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? sellerId = null)
        {
            var query = _context.Sales
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems).ThenInclude(i => i.Product)
                .Include(s => s.SalesPayments)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(s => s.SoldAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.SoldAt <= endDate.Value);

            if (sellerId.HasValue)
                query = query.Where(s => s.SellerUserId == sellerId.Value);

            var sales = await query
                .OrderByDescending(s => s.SoldAt)
                .ToListAsync();

            return sales.Select(MapSaleToDto);
        }

        // ==========================================================
        // GET SALE BY ID
        // ==========================================================
        public async Task<SaleDto?> GetSaleByIdAsync(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems).ThenInclude(i => i.Product)
                .Include(s => s.SalesPayments)
                .FirstOrDefaultAsync(s => s.Id == id);

            return sale == null ? null : MapSaleToDto(sale);
        }

        // ==========================================================
        // CREATE SALE
        // ==========================================================
        public async Task<SaleDto> CreateSaleAsync(CreateSaleDto dto)
        {
            using var trx = await _context.Database.BeginTransactionAsync();

            try
            {
                var seller = await _context.Users.FindAsync(dto.SellerUserId)
                    ?? throw new Exception("Vendedor no encontrado.");

                var cashSession = await _context.CashSessions.FindAsync(dto.CashSessionId)
                    ?? throw new Exception("Sesión de caja no encontrada.");

                decimal subtotal = 0;
                decimal discountTotal = 0;

                foreach (var item in dto.Items)
                {
                    subtotal += item.UnitPrice * item.Quantity;
                    discountTotal += item.Discount;
                }

                decimal total = subtotal - discountTotal;

                // ==================================================
                // VALIDAR PAGO
                // ==================================================
                decimal totalPayments = dto.Payments.Sum(p => p.Amount);
                if (totalPayments < total)
                    throw new Exception("Los pagos no cubren el total.");

                // ==================================================
                // CREAR CASH MOVEMENT
                // ==================================================
                var movement = new CashMovement
                {
                    CashSessionId = dto.CashSessionId,
                    Amount = total,
                    Type = CashMovementType.Sale,
                    Description = "Venta",
                    CreationDate = DateTime.Now
                };

                _context.CashMovements.Add(movement);
                await _context.SaveChangesAsync();

                // ==================================================
                // CREAR VENTA
                // ==================================================
                var sale = new Sale
                {
                    CashMovementId = movement.Id,
                    SellerUserId = dto.SellerUserId,
                    Subtotal = subtotal,
                    DiscountTotal = discountTotal,
                    Total = total,
                    SoldAt = DateTime.Now,

                    DeliveryType = dto.DeliveryType,
                    DeliveryAddress = dto.DeliveryAddress,
                    DeliveryCost = dto.DeliveryCost,
                    DeliveryNote = dto.DeliveryNote,

                    PaymentStatus = 1, // Pagado (ya que pagaron todo)
                    CreationDate = DateTime.Now
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // ==================================================
                // ITEMS + RESTAR STOCK
                // ==================================================
                foreach (var itemDto in dto.Items)
                {
                    var product = await _context.Products.FindAsync(itemDto.ProductId)
                        ?? throw new Exception($"Producto {itemDto.ProductId} no encontrado.");

                    _context.SalesItems.Add(new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = itemDto.ProductId,
                        Quantity = itemDto.Quantity,
                        UnitPrice = itemDto.UnitPrice,
                        Discount = itemDto.Discount,
                        ProductUnitId = itemDto.ProductUnitId,
                        ConversionToBase = 1,
                        DeductedBaseQuantity = itemDto.Quantity,
                        CreationDate = DateTime.Now
                    });

                    // RESTAR STOCK
                    var stock = await _context.ProductsStocks
                        .FirstOrDefaultAsync(s =>
                            s.ProductId == itemDto.ProductId &&
                            s.BranchId == cashSession.BranchId);

                    if (stock == null)
                        throw new Exception($"No hay stock del producto {product.Name}.");

                    stock.Quantity -= itemDto.Quantity;

                    if (stock.Quantity < 0)
                        throw new Exception($"Stock insuficiente de {product.Name}.");
                }

                // ==================================================
                // PAGOS
                // ==================================================
                foreach (var pay in dto.Payments)
                {
                    _context.SalesPayments.Add(new SalePayment
                    {
                        SaleId = sale.Id,
                        Method = (PaymentMethod)pay.Method,
                        Amount = pay.Amount,
                        Reference = pay.Reference,
                        CreationDate = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                await trx.CommitAsync();

                return (await GetSaleByIdAsync(sale.Id))!;
            }
            catch
            {
                await trx.RollbackAsync();
                throw;
            }
        }

        // ==========================================================
        // SUMMARY TODAY
        // ==========================================================
        public async Task<object> GetTodaySalesSummaryAsync()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var sales = await _context.Sales
                .Where(s => s.SoldAt >= today && s.SoldAt < tomorrow)
                .ToListAsync();

            return new
            {
                Date = today,
                TotalSales = sales.Count,
                TotalAmount = sales.Sum(x => x.Total),
                TotalDiscount = sales.Sum(x => x.DiscountTotal),
                Average = sales.Any() ? sales.Average(x => x.Total) : 0
            };
        }

        // ==========================================================
        // MAP SALE TO DTO
        // ==========================================================
        private SaleDto MapSaleToDto(Sale s)
        {
            return new SaleDto
            {
                Id = s.Id,
                SoldAt = s.SoldAt,
                SellerName = $"{s.SellerUser.Name} {s.SellerUser.LastName}",
                Subtotal = s.Subtotal,
                DiscountTotal = s.DiscountTotal,
                Total = s.Total,

                DeliveryType = s.DeliveryType,
                DeliveryAddress = s.DeliveryAddress,
                DeliveryCost = s.DeliveryCost,
                DeliveryNote = s.DeliveryNote,
                PaymentStatus = s.PaymentStatus,

                Items = s.SalesItems.Select(i => new SaleItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Discount = i.Discount,
                    Total = (i.Quantity * i.UnitPrice) - i.Discount
                }).ToList(),

                Payments = s.SalesPayments.Select(p => new SalePaymentDto
                {
                    Method = (int)p.Method,
                    MethodName = p.Method.ToString(),
                    Amount = p.Amount,
                    Reference = p.Reference
                }).ToList()
            };
        }
    }
}
