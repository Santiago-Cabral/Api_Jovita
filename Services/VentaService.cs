}
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

        // =====================================================
        // GET ALL
        // =====================================================
        public async Task<IEnumerable<SaleDto>> GetAllSalesAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? sellerId = null)
        {
            var query = _context.Sales.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(s => s.SoldAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.SoldAt <= endDate.Value);

            if (sellerId.HasValue)
                query = query.Where(s => s.SellerUserId == sellerId.Value);

            var sales = await query
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems).ThenInclude(i => i.Product)
                .Include(s => s.SalesPayments)
                .OrderByDescending(s => s.SoldAt)
                .ToListAsync();

            return sales.Select(MapSaleToDto);
        }

        // =====================================================
        // GET BY ID
        // =====================================================
        public async Task<SaleDto?> GetSaleByIdAsync(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems).ThenInclude(i => i.Product)
                .Include(s => s.SalesPayments)
                .FirstOrDefaultAsync(s => s.Id == id);

            return sale == null ? null : MapSaleToDto(sale);
        }

        // =====================================================
        // 🟢 VENTA WEB (DESCUENTA STOCK CASA CENTRAL)
        // =====================================================
        public async Task<SaleDto> CreatePublicSaleAsync(CreatePublicSaleDto dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                if (dto.Items == null || !dto.Items.Any())
                    throw new InvalidOperationException("El pedido no tiene productos.");

                // 1. VALIDAR STOCK
                foreach (var item in dto.Items)
                {
                    var stock = await _context.ProductsStocks
                        .FirstOrDefaultAsync(s =>
                            s.ProductId == item.ProductId &&
                            s.BranchId == CASA_CENTRAL_BRANCH_ID);

                    if (stock == null || stock.Quantity < item.Quantity)
                        throw new InvalidOperationException(
                            $"Stock insuficiente para producto {item.ProductId}");
                }

                // 2. USUARIO SISTEMA
                var systemUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == "admin@jovita.com")
                    ?? throw new InvalidOperationException("Usuario sistema no encontrado");

                var cashSession = await _context.CashSessions
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync()
                    ?? throw new InvalidOperationException("No hay sesión de caja activa");

                // 3. TOTALES
                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal total = subtotal + dto.ShippingCost;

                // 4. CASH MOVEMENT
                var cashMovement = new CashMovement
                {
                    CashSessionId = cashSession.Id,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    Description = "Venta Web",
                    CreationDate = DateTime.Now,
                    TypeOfSale = "Web"
                };

                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                // 5. SALE
                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SellerUserId = systemUser.Id,
                    SoldAt = DateTime.Now,
                    Subtotal = subtotal,
                    DiscountTotal = 0,
                    Total = total,
                    DeliveryType = 1,
                    DeliveryAddress = dto.Customer,
                    DeliveryCost = dto.ShippingCost,
                    DeliveryNote = dto.PaymentReference,
                    PaymentStatus = 1,
                    CreationDate = DateTime.Now
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // 6. ITEMS + DESCUENTO STOCK
                foreach (var item in dto.Items)
                {
                    var stock = await _context.ProductsStocks.FirstAsync(s =>
                        s.ProductId == item.ProductId &&
                        s.BranchId == CASA_CENTRAL_BRANCH_ID);

                    stock.Quantity -= item.Quantity;

                    _context.SalesItems.Add(new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Discount = 0,
                        ConversionToBase = 1,
                        DeductedBaseQuantity = item.Quantity,
                        CreationDate = DateTime.Now
                    });
                }

                // 7. PAYMENT
                _context.SalesPayments.Add(new SalePayment
                {
                    SaleId = sale.Id,
                    Method = PaymentMethod.Transfer,
                    Amount = total,
                    Reference = dto.PaymentReference ?? "Web",
                    CreationDate = DateTime.Now
                });

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

        // =====================================================
        // VENTA INTERNA (YA TENÍAS BIEN)
        // =====================================================
        public async Task<SaleDto> CreateSaleAsync(CreateSaleDto dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                var cashSession = await _context.CashSessions.FindAsync(dto.CashSessionId)
                    ?? throw new InvalidOperationException("Caja inválida");

                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal total = subtotal - dto.Items.Sum(i => i.Discount);

                var cashMovement = new CashMovement
                {
                    CashSessionId = cashSession.Id,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    Description = "Venta Caja",
                    CreationDate = DateTime.Now
                };

                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SellerUserId = dto.SellerUserId,
                    SoldAt = DateTime.Now,
                    Subtotal = subtotal,
                    DiscountTotal = subtotal - total,
                    Total = total,
                    PaymentStatus = 1,
                    CreationDate = DateTime.Now
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in dto.Items)
                {
                    var stock = await _context.ProductsStocks.FirstAsync(s =>
                        s.ProductId == item.ProductId &&
                        s.BranchId == cashSession.BranchId);

                    if (stock.Quantity < item.Quantity)
                        throw new InvalidOperationException("Stock insuficiente");

                    stock.Quantity -= item.Quantity;

                    _context.SalesItems.Add(new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Discount = item.Discount,
                        ConversionToBase = 1,
                        DeductedBaseQuantity = item.Quantity,
                        CreationDate = DateTime.Now
                    });
                }

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
                await tx.CommitAsync();

                return (await GetSaleByIdAsync(sale.Id))!;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // =====================================================
        // MAP
        // =====================================================
        private static SaleDto MapSaleToDto(Sale s)
        {
            return new SaleDto
            {
                Id = s.Id,
                SoldAt = s.SoldAt,
                SellerName = s.SellerUser != null
                    ? $"{s.SellerUser.Name} {s.SellerUser.LastName}"
                    : "Web",
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
                    ProductName = i.Product?.Name ?? "Producto",
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
                    Reference = p.Reference ?? ""
                }).ToList()
            };
        }
    }
}
