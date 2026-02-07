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
        private const int CASA_CENTRAL_BRANCH_ID = 1;

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
                .Where(s => !s.IsDeleted)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(s => s.SoldAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.SoldAt <= endDate.Value);

            if (sellerId.HasValue)
                query = query.Where(s => s.SellerUserId == sellerId.Value);

            var sales = await query
                .Include(s => s.Client)
                .Include(s => s.SellerUser)
                .Include(s => s.SalesItems).ThenInclude(i => i.Product)
                .Include(s => s.SalesPayments)
                .OrderByDescending(s => s.SoldAt)
                .ToListAsync();

            return sales.Select(MapSaleToDto);
        }

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

        public async Task<SaleDto> CreateSaleAsync(CreateSaleDto dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Validar y descontar stock
                foreach (var item in dto.Items)
                {
                    var stock = await _context.ProductsStocks.FirstOrDefaultAsync(s =>
                        s.ProductId == item.ProductId &&
                        s.BranchId == CASA_CENTRAL_BRANCH_ID);

                    if (stock == null || stock.Quantity < item.Quantity)
                        throw new InvalidOperationException(
                            $"Stock insuficiente para producto {item.ProductId}");

                    stock.Quantity -= item.Quantity;
                }

                // 2. Usuario y caja
                var user = await _context.Users.FirstAsync();
                var cashSession = await _context.CashSessions
                    .OrderByDescending(c => c.Id)
                    .FirstAsync();

                // 3. Totales
                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal discountTotal = dto.Items.Sum(i => i.Discount);
                decimal total = subtotal - discountTotal;

                // 4. Movimiento de caja
                var cashMovement = new CashMovement
                {
                    CashSessionId = cashSession.Id,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    CreationDate = DateTime.UtcNow
                };

                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                // 5. Venta
                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    ClientId = dto.ClientId,
                    SellerUserId = user.Id,
                    SoldAt = DateTime.UtcNow,
                    Subtotal = subtotal,
                    DiscountTotal = discountTotal,
                    Total = total,
                    PaymentStatus = (dto.Payments != null && dto.Payments.Any()) ? 1 : 0,
                    CreationDate = DateTime.UtcNow
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // 6. Items
                foreach (var item in dto.Items)
                {
                    _context.SalesItems.Add(new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Discount = item.Discount,
                        CreationDate = DateTime.UtcNow
                    });
                }

                // 7. Pagos (CORREGIDO)
                if (dto.Payments != null && dto.Payments.Any())
                {
                    foreach (var payment in dto.Payments)
                    {
                        if (!Enum.IsDefined(typeof(PaymentMethod), payment.Method))
                            throw new InvalidOperationException(
                                $"Método de pago inválido: {payment.Method}");

                        var method = (PaymentMethod)payment.Method;

                        _context.SalesPayments.Add(new SalePayment
                        {
                            SaleId = sale.Id,
                            Method = method,
                            Amount = payment.Amount,
                            Reference = payment.Reference,
                            CreationDate = DateTime.UtcNow
                        });
                    }
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

            if (!string.IsNullOrWhiteSpace(dto.PaymentMethod))
                sale.PaymentMethod = dto.PaymentMethod;

            if (!string.IsNullOrWhiteSpace(dto.FulfillmentMethod))
                sale.FulfillmentMethod = dto.FulfillmentMethod;

            await _context.SaveChangesAsync();
            return await GetSaleByIdAsync(id);
        }

        public async Task<bool> DeleteSaleAsync(int id)
        {
            var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null) return false;

            sale.IsDeleted = true;
            await _context.SaveChangesAsync();
            return true;
        }

        private static SaleDto MapSaleToDto(Sale s)
        {
            return new SaleDto
            {
                Id = s.Id,
                SoldAt = s.SoldAt,
                SellerName = s.SellerUser?.UserName ?? "E-commerce",
                Subtotal = s.Subtotal,
                DiscountTotal = s.DiscountTotal,
                Total = s.Total,
                ClientId = s.ClientId,
                ClientName = s.Client?.FullName,
                DeliveryAddress = s.DeliveryAddress,
                DeliveryCost = s.DeliveryCost,
                PaymentStatus = s.PaymentStatus,
                PaymentMethod = s.PaymentMethod,
                FulfillmentMethod = s.FulfillmentMethod,
                Items = s.SalesItems?.Select(i => new SaleItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? "",
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                    Discount = i.Discount,
                    Total = (i.UnitPrice * i.Quantity) - i.Discount
                }).ToList() ?? new(),
                Payments = s.SalesPayments?.Select(p => new SalePaymentDto
                {
                    MethodName = p.Method.ToString(),
                    Amount = p.Amount,
                    Reference = p.Reference
                }).ToList() ?? new()
            };
        }
    }
}
