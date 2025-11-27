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
        // CREATE SALE
        // ============================================================
        public async Task<SaleDto> CreateSaleAsync(CreateSaleDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ------- Validar vendedor -------
                var seller = await _context.Users.FindAsync(dto.SellerUserId);
                if (seller == null)
                    throw new InvalidOperationException("Vendedor no encontrado.");

                // ------- Validar sesión de caja -------
                var cashSession = await _context.CashSessions.FindAsync(dto.CashSessionId);
                if (cashSession == null)
                    throw new InvalidOperationException("Sesión de caja no encontrada.");

                // ------- Calcular totales -------
                decimal subtotal = 0;
                decimal totalDiscount = 0;

                foreach (var item in dto.Items)
                {
                    subtotal += item.Quantity * item.UnitPrice;
                    totalDiscount += item.Discount;
                }

                decimal total = subtotal - totalDiscount;

                // ------- Validar pagos -------
                decimal totalPayments = dto.Payments.Sum(p => p.Amount);

                if (totalPayments < total)
                    throw new InvalidOperationException("Los pagos no cubren el total de la venta.");

                // ------- Registrar movimiento de caja -------
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

                // ------- Crear venta -------
                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SoldAt = DateTime.Now,
                    SellerUserId = dto.SellerUserId,
                    Subtotal = subtotal,
                    DiscountTotal = totalDiscount,
                    Total = total,
                    CreationDate = DateTime.Now,
                    DeliveryType = null,
                    DeliveryAddress = null,
                    DeliveryCost = null,
                    DeliveryNote = null,
                    PaymentStatus = 1 // pagado (1 = pagado, 0 = pendiente)
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // ------- Crear ítems y actualizar stock -------
                foreach (var item in dto.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                        throw new InvalidOperationException($"Producto {item.ProductId} no encontrado.");

                    var saleItem = new SaleItem
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
                    };

                    _context.SalesItems.Add(saleItem);

                    // Actualizar stock
                    var stock = await _context.ProductsStocks
                        .FirstOrDefaultAsync(s =>
                            s.ProductId == item.ProductId &&
                            s.BranchId == cashSession.BranchId);

                    if (stock == null)
                        throw new InvalidOperationException($"No existe stock para el producto {product.Name} en esta sucursal.");

                    stock.Quantity = stock.Quantity - item.Quantity;


                    if (stock.Quantity < 0)
                        throw new InvalidOperationException($"Stock insuficiente para {product.Name}.");
                }

                // ------- Registrar pagos -------
                foreach (var pay in dto.Payments)
                {
                    var salePayment = new SalePayment
                    {
                        SaleId = sale.Id,
                        Method = (PaymentMethod)pay.Method,
                        Amount = pay.Amount,
                        Reference = pay.Reference,
                        CreationDate = DateTime.Now
                    };

                    _context.SalesPayments.Add(salePayment);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // ------- Devolver venta completa -------
                return (await GetSaleByIdAsync(sale.Id))!;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        // RESUMEN DEL DÍA
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
        // MAP: Sale -> SaleDto
        // ============================================================
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