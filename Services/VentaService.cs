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

        public async Task<IEnumerable<SaleDto>> GetAllSalesAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? sellerId = null)
        {
            var query = _context.Sales
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems)
                    .ThenInclude(si => si.Product)
                .Include(s => s.SalesPayments)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(s => s.SoldAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.SoldAt <= endDate.Value);

            if (sellerId.HasValue)
                query = query.Where(s => s.SellerUserId == sellerId.Value);

            var sales = await query.OrderByDescending(s => s.SoldAt).ToListAsync();

            return sales.Select(s => new SaleDto
            {
                Id = s.Id,
                SoldAt = s.SoldAt,
                SellerName = $"{s.SellerUser.Name} {s.SellerUser.LastName}",
                Subtotal = s.Subtotal,
                DiscountTotal = s.DiscountTotal,
                Total = s.Total,
                Items = s.SalesItems.Select(si => new SaleItemDto
                {
                    ProductId = si.ProductId,
                    ProductName = si.Product.Name,
                    Quantity = si.Quantity,
                    UnitPrice = si.UnitPrice,
                    Discount = si.Discount,
                    Total = (si.Quantity * si.UnitPrice) - si.Discount
                }).ToList(),
                Payments = s.SalesPayments.Select(sp => new SalePaymentDto
                {
                    Method = (int)sp.Method,
                    MethodName = sp.Method.ToString(),
                    Amount = sp.Amount,
                    Reference = sp.Reference
                }).ToList()
            });
        }

        public async Task<SaleDto?> GetSaleByIdAsync(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems)
                    .ThenInclude(si => si.Product)
                .Include(s => s.SalesPayments)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return null;

            return new SaleDto
            {
                Id = sale.Id,
                SoldAt = sale.SoldAt,
                SellerName = $"{sale.SellerUser.Name} {sale.SellerUser.LastName}",
                Subtotal = sale.Subtotal,
                DiscountTotal = sale.DiscountTotal,
                Total = sale.Total,
                Items = sale.SalesItems.Select(si => new SaleItemDto
                {
                    ProductId = si.ProductId,
                    ProductName = si.Product.Name,
                    Quantity = si.Quantity,
                    UnitPrice = si.UnitPrice,
                    Discount = si.Discount,
                    Total = (si.Quantity * si.UnitPrice) - si.Discount
                }).ToList(),
                Payments = sale.SalesPayments.Select(sp => new SalePaymentDto
                {
                    Method = (int)sp.Method,
                    MethodName = sp.Method.ToString(),
                    Amount = sp.Amount,
                    Reference = sp.Reference
                }).ToList()
            };
        }

        public async Task<SaleDto> CreateSaleAsync(CreateSaleDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validar que el vendedor existe
                var seller = await _context.Users.FindAsync(dto.SellerUserId);
                if (seller == null)
                    throw new InvalidOperationException("Vendedor no encontrado");

                // Validar que la sesión de caja existe
                var cashSession = await _context.CashSessions.FindAsync(dto.CashSessionId);
                if (cashSession == null)
                    throw new InvalidOperationException("Sesión de caja no encontrada");

                // Calcular totales
                decimal subtotal = 0;
                decimal discountTotal = 0;

                foreach (var item in dto.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                        throw new InvalidOperationException($"Producto {item.ProductId} no encontrado");

                    subtotal += item.Quantity * item.UnitPrice;
                    discountTotal += item.Discount;
                }

                decimal total = subtotal - discountTotal;

                // Validar que los pagos cubren el total
                decimal totalPayments = dto.Payments.Sum(p => p.Amount);
                if (totalPayments < total)
                    throw new InvalidOperationException("Los pagos no cubren el total de la venta");

                // Crear movimiento de caja
                var cashMovement = new CashMovement
                {
                    CashSessionId = dto.CashSessionId,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    Description = "Venta",
                    CreationDate = DateTime.Now
                };
                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                // Crear venta
                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SoldAt = DateTime.Now,
                    SellerUserId = dto.SellerUserId,
                    Subtotal = subtotal,
                    DiscountTotal = discountTotal,
                    Total = total,
                    CreationDate = DateTime.Now
                };
                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Crear items de venta y actualizar stock
                foreach (var itemDto in dto.Items)
                {
                    var product = await _context.Products.FindAsync(itemDto.ProductId);

                    var saleItem = new SaleItem
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
                    };
                    _context.SalesItems.Add(saleItem);

                    // Actualizar stock
                    var stock = await _context.ProductsStocks
                        .FirstOrDefaultAsync(s =>
                            s.ProductId == itemDto.ProductId &&
                            s.BranchId == cashSession.BranchId);

                    if (stock != null)
                    {
                        stock.Quantity -= itemDto.Quantity;
                        if (stock.Quantity < 0)
                            throw new InvalidOperationException($"Stock insuficiente para {product!.Name}");
                    }
                }

                // Crear pagos
                foreach (var paymentDto in dto.Payments)
                {
                    var payment = new SalePayment
                    {
                        SaleId = sale.Id,
                        Method = (PaymentMethod)paymentDto.Method,
                        Amount = paymentDto.Amount,
                        Reference = paymentDto.Reference,
                        CreationDate = DateTime.Now
                    };
                    _context.SalesPayments.Add(payment);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Recargar la venta con todas las relaciones
                return (await GetSaleByIdAsync(sale.Id))!;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

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
                TotalAmount = sales.Sum(s => s.Total),
                TotalDiscount = sales.Sum(s => s.DiscountTotal),
                AverageTicket = sales.Any() ? sales.Average(s => s.Total) : 0
            };
        }
    }
}