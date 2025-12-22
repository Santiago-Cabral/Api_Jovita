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

        // ============================================================
        // GET ALL SALES
        // ============================================================
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

        // ============================================================
        // GET SALE BY ID
        // ============================================================
        public async Task<SaleDto?> GetSaleByIdAsync(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems).ThenInclude(i => i.Product)
                .Include(s => s.SalesPayments)
                .FirstOrDefaultAsync(s => s.Id == id);

            return sale == null ? null : MapSaleToDto(sale);
        }

        // ============================================================
        // CREATE PUBLIC SALE (WEB / CARRITO)
        // ============================================================
        public async Task<SaleDto> CreatePublicSaleAsync(CreatePublicSaleDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal total = subtotal + dto.ShippingCost;

                var sale = new Sale
                {
                    SoldAt = DateTime.Now,
                    Subtotal = subtotal,
                    DiscountTotal = 0,
                    Total = total,

                    DeliveryType = 1,
                    DeliveryAddress = dto.Customer.Address,
                    DeliveryCost = dto.ShippingCost,
                    DeliveryNote = $"{dto.Customer.Name} | {dto.Customer.Phone}",

                    // 0 = Pendiente (Payway confirmará luego)
                    PaymentStatus = 0,
                    CreationDate = DateTime.Now
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in dto.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                        throw new InvalidOperationException("Producto no encontrado.");

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

                // Pago web (Payway / Transferencia / etc.)
                _context.SalesPayments.Add(new SalePayment
                {
                    SaleId = sale.Id,
                    Method = (PaymentMethod)dto.PaymentMethod,
                    Amount = total,
                    Reference = dto.PaymentReference ?? "Pedido Web",
                    CreationDate = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (await GetSaleByIdAsync(sale.Id))!;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        // CREATE SALE (CAJA / INTERNA)
        // ============================================================
        public async Task<SaleDto> CreateSaleAsync(CreateSaleDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var seller = await _context.Users.FindAsync(dto.SellerUserId);
                if (seller == null)
                    throw new InvalidOperationException("Vendedor no encontrado.");

                var cashSession = await _context.CashSessions.FindAsync(dto.CashSessionId);
                if (cashSession == null)
                    throw new InvalidOperationException("Sesión de caja no encontrada.");

                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal totalDiscount = dto.Items.Sum(i => i.Discount);
                decimal total = subtotal - totalDiscount;

                if (dto.Payments.Sum(p => p.Amount) < total)
                    throw new InvalidOperationException("Los pagos no cubren el total.");

                var cashMovement = new CashMovement
                {
                    CashSessionId = dto.CashSessionId,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    Description = "Venta realizada",
                    CreationDate = DateTime.Now
                };

                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SoldAt = DateTime.Now,
                    SellerUserId = dto.SellerUserId,
                    Subtotal = subtotal,
                    DiscountTotal = totalDiscount,
                    Total = total,
                    PaymentStatus = 1,
                    CreationDate = DateTime.Now
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in dto.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId)
                        ?? throw new InvalidOperationException("Producto no encontrado.");

                    _context.SalesItems.Add(new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Discount = item.Discount,
                        ProductUnitId = item.ProductUnitId,
                        ConversionToBase = 1,
                        DeductedBaseQuantity = item.Quantity,
                        CreationDate = DateTime.Now
                    });

                    var stock = await _context.ProductsStocks.FirstOrDefaultAsync(s =>
                        s.ProductId == item.ProductId &&
                        s.BranchId == cashSession.BranchId);

                    if (stock == null || stock.Quantity < item.Quantity)
                        throw new InvalidOperationException($"Stock insuficiente para {product.Name}");

                    stock.Quantity -= item.Quantity;
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
                await transaction.CommitAsync();

                return (await GetSaleByIdAsync(sale.Id))!;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        // UPDATE SALE
        // ============================================================
        public async Task<SaleDto?> UpdateSaleAsync(int id, UpdateSaleDto dto)
        {
            var sale = await _context.Sales
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems).ThenInclude(i => i.Product)
                .Include(s => s.SalesPayments)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null) return null;

            if (dto.DeliveryType.HasValue) sale.DeliveryType = dto.DeliveryType;
            if (!string.IsNullOrWhiteSpace(dto.DeliveryAddress)) sale.DeliveryAddress = dto.DeliveryAddress;
            if (dto.DeliveryCost.HasValue) sale.DeliveryCost = dto.DeliveryCost;
            if (!string.IsNullOrWhiteSpace(dto.DeliveryNote)) sale.DeliveryNote = dto.DeliveryNote;
            if (dto.PaymentStatus.HasValue) sale.PaymentStatus = dto.PaymentStatus;

            await _context.SaveChangesAsync();
            return MapSaleToDto(sale);
        }

        // ============================================================
        // TODAY SUMMARY
        // ============================================================
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
                AverageTicket = sales.Any() ? sales.Average(s => s.Total) : 0m
            };
        }

        // ============================================================
        // MAP ENTITY -> DTO
        // ============================================================
        private SaleDto MapSaleToDto(Sale s)
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
