using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                .AsQueryable()
                .AsNoTracking();

            if (startDate.HasValue)
                query = query.Where(s => s.SoldAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.SoldAt <= endDate.Value);

            if (sellerId.HasValue)
                query = query.Where(s => s.SellerUserId == sellerId.Value);

            var salesEntities = await query
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems).ThenInclude(i => i.Product)
                .Include(s => s.SalesPayments)
                .OrderByDescending(s => s.SoldAt)
                .ToListAsync();

            return salesEntities.Select(s => MapSaleToDto(s)).ToList();
        }

        // ============================================================
        // GET SALE BY ID
        // ============================================================
        public async Task<SaleDto?> GetSaleByIdAsync(int id)
        {
            var sale = await _context.Sales
                .AsNoTracking()
                .Where(s => s.Id == id)
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems).ThenInclude(i => i.Product)
                .Include(s => s.SalesPayments)
                .FirstOrDefaultAsync();

            if (sale == null) return null;
            return MapSaleToDto(sale);
        }

        // ============================================================
        // CREATE PUBLIC SALE (web)
        // ============================================================
        public async Task<SaleDto> CreatePublicSaleAsync(CreatePublicSaleDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (dto.Items == null || !dto.Items.Any())
                    throw new InvalidOperationException("El pedido debe incluir productos.");

                foreach (var it in dto.Items)
                {
                    if (it.Quantity <= 0)
                        throw new InvalidOperationException($"Quantity inválida para producto {it.ProductId}.");
                    if (it.UnitPrice < 0)
                        throw new InvalidOperationException($"UnitPrice inválido para producto {it.ProductId}.");
                }

                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal discountTotal = 0m;
                decimal total = subtotal + dto.ShippingCost - discountTotal;
                if (total < 0) total = 0m;

                var systemUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == "admin@jovita.com" || u.UserName == "admin@jovita.com");

                if (systemUser == null)
                    systemUser = await _context.Users.FirstOrDefaultAsync(u => u.RoleId == 1);

                if (systemUser == null)
                    throw new InvalidOperationException("No se encontró usuario sistema (admin).");

                var activeSession = await _context.CashSessions
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                if (activeSession == null)
                    throw new InvalidOperationException("No se encontró CashSession. Crear/activar una sesión de caja.");

                var cashMovement = new CashMovement
                {
                    CashSessionId = activeSession.Id,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    Description = $"Venta Web - {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {dto.PaymentReference ?? "Pedido Web"}",
                    CreationDate = DateTime.Now,
                    TypeOfSale = "Web",
                    MovementCancelled = false
                };

                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SellerUserId = systemUser.Id,
                    SoldAt = DateTime.Now,
                    Subtotal = subtotal,
                    DiscountTotal = discountTotal,
                    Total = total,
                    DeliveryType = 1,
                    DeliveryAddress = dto.Customer,
                    DeliveryCost = dto.ShippingCost,
                    DeliveryNote = dto.PaymentReference ?? "Pedido Web",
                    // 🔧 FIX: Asignar decimal directamente
                    PaymentStatus = 0m,
                    CreationDate = DateTime.Now
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in dto.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                        throw new InvalidOperationException($"Producto {item.ProductId} no encontrado.");

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

                decimal paymentsSum = 0m;
                PaymentMethod paymentMethod = PaymentMethod.Cash;
                if (!string.IsNullOrWhiteSpace(dto.PaymentMethod))
                {
                    var pmLower = dto.PaymentMethod.ToString().ToLower();
                    paymentMethod = pmLower switch
                    {
                        "cash" => PaymentMethod.Cash,
                        "card" => PaymentMethod.Card,
                        "credit" => PaymentMethod.Credit,
                        "transfer" => PaymentMethod.Transfer,
                        _ => PaymentMethod.Cash
                    };
                }

                if (total > 0)
                {
                    var salePayment = new SalePayment
                    {
                        SaleId = sale.Id,
                        Method = paymentMethod,
                        Amount = total,
                        Reference = dto.PaymentReference ?? "Pedido Web",
                        CreationDate = DateTime.Now
                    };
                    _context.SalesPayments.Add(salePayment);
                    paymentsSum += total;
                }

                await _context.SaveChangesAsync();

                // 🔧 FIX: Actualizar estado con decimal
                if (paymentsSum >= total && total > 0)
                    sale.PaymentStatus = 1m;  // Pagado
                else if (paymentsSum > 0 && paymentsSum < total)
                    sale.PaymentStatus = 2m;  // Parcial
                else
                    sale.PaymentStatus = 0m;  // Pendiente

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
        // CREATE SALE (caja interna)
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
                    // 🔧 FIX: decimal directo
                    PaymentStatus = 1m,
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

            if (dto.DeliveryType.HasValue)
                sale.DeliveryType = dto.DeliveryType.Value;

            if (!string.IsNullOrWhiteSpace(dto.DeliveryAddress))
                sale.DeliveryAddress = dto.DeliveryAddress;

            if (dto.DeliveryCost.HasValue)
                sale.DeliveryCost = dto.DeliveryCost.Value;

            if (!string.IsNullOrWhiteSpace(dto.DeliveryNote))
                sale.DeliveryNote = dto.DeliveryNote;

            if (dto.PaymentStatus.HasValue)
            {
                // 🔧 FIX: Conversión explícita int -> decimal
                sale.PaymentStatus = (decimal)dto.PaymentStatus.Value;
            }

            await _context.SaveChangesAsync();
            return MapSaleToDto(sale);
        }

        // ============================================================
        // UPDATE ONLY SALE STATUS
        // ============================================================
        public async Task<SaleDto?> UpdateSaleStatusAsync(int id, int status)
        {
            var sale = new Sale { Id = id };
            _context.Sales.Attach(sale);

            // 🔧 FIX: Conversión explícita int -> decimal
            sale.PaymentStatus = (decimal)status;
            _context.Entry(sale).Property(s => s.PaymentStatus).IsModified = true;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                throw;
            }

            return await GetSaleByIdAsync(id);
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
                SellerName = s.SellerUser != null ? $"{s.SellerUser.Name} {s.SellerUser.LastName}" : "Web",
                Subtotal = s.Subtotal,
                DiscountTotal = s.DiscountTotal,
                Total = s.Total,
                DeliveryType = s.DeliveryType,
                DeliveryAddress = s.DeliveryAddress,
                DeliveryCost = s.DeliveryCost,
                DeliveryNote = s.DeliveryNote,
                // 🔧 FIX: Conversión segura decimal -> int
                PaymentStatus = (int)s.PaymentStatus,
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
                    Reference = p.Reference ?? string.Empty
                }).ToList()
            };
        }
    }
}
