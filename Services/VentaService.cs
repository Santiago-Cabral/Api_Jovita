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

        public async Task<IEnumerable<SaleDto>> GetAllSalesAsync(DateTime? startDate = null, DateTime? endDate = null, int? sellerId = null)
        {
            var query = _context.Sales.Where(s => !s.IsDeleted).AsQueryable();

            if (startDate.HasValue) query = query.Where(s => s.SoldAt >= startDate.Value);
            if (endDate.HasValue) query = query.Where(s => s.SoldAt <= endDate.Value);
            if (sellerId.HasValue) query = query.Where(s => s.SellerUserId == sellerId.Value);

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

        public async Task<SaleDto> CreatePublicSaleAsync(CreatePublicSaleDto dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Customer))
                    throw new ArgumentException("Customer (dirección) es requerido.");

                // Validar y descontar stock
                foreach (var item in dto.Items)
                {
                    var stock = await _context.ProductsStocks.FirstOrDefaultAsync(s =>
                        s.ProductId == item.ProductId &&
                        s.BranchId == CASA_CENTRAL_BRANCH_ID);

                    if (stock == null || stock.Quantity < item.Quantity)
                        throw new InvalidOperationException($"Stock insuficiente para producto {item.ProductId}");

                    stock.Quantity -= item.Quantity;
                }

                var user = await _context.Users.FirstAsync();
                var cashSession = await _context.CashSessions.OrderByDescending(c => c.Id).FirstAsync();

                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal total = subtotal + dto.ShippingCost;

                var cashMovement = new CashMovement
                {
                    CashSessionId = cashSession.Id,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    CreationDate = DateTime.UtcNow
                };

                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                Client? client = null;
                if (!string.IsNullOrWhiteSpace(dto.Email) || !string.IsNullOrWhiteSpace(dto.Phone))
                {
                    if (!string.IsNullOrWhiteSpace(dto.Email))
                        client = await _context.Clients.FirstOrDefaultAsync(c => !c.IsDeleted && c.Email == dto.Email);

                    if (client == null && !string.IsNullOrWhiteSpace(dto.Phone))
                        client = await _context.Clients.FirstOrDefaultAsync(c => !c.IsDeleted && c.Phone == dto.Phone);

                    if (client == null)
                    {
                        client = new Client
                        {
                            FullName = !string.IsNullOrWhiteSpace(dto.Email) ? dto.Email! :
                                       (!string.IsNullOrWhiteSpace(dto.Phone) ? dto.Phone! : "Cliente web"),
                            Email = dto.Email,
                            Phone = dto.Phone ?? string.Empty,
                            Amount = 0,
                            DebitBalance = 0,
                            IsDeleted = false,
                            CreationDate = DateTime.UtcNow
                        };

                        _context.Clients.Add(client);
                        await _context.SaveChangesAsync();
                    }
                }

                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SellerUserId = user.Id,
                    SoldAt = DateTime.UtcNow,
                    Subtotal = subtotal,
                    DiscountTotal = 0,
                    CustomerName = dto.Customer,
                    Total = total,
                    PaymentStatus = 0,
                    CreationDate = DateTime.UtcNow,
                    DeliveryAddress = dto.Customer,
                    DeliveryCost = dto.ShippingCost,
                    PaymentMethod = dto.PaymentMethod,
                    FulfillmentMethod = dto.FulfillmentMethod
                };

                if (client != null) sale.ClientId = client.Id;

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in dto.Items)
                {
                    _context.SalesItems.Add(new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Discount = 0,
                        CreationDate = DateTime.UtcNow
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

        public async Task<SaleDto> CreateSaleAsync(CreateSaleDto dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validar y descontar stock
                foreach (var item in dto.Items)
                {
                    var stock = await _context.ProductsStocks.FirstOrDefaultAsync(s =>
                        s.ProductId == item.ProductId &&
                        s.BranchId == CASA_CENTRAL_BRANCH_ID);

                    if (stock == null || stock.Quantity < item.Quantity)
                        throw new InvalidOperationException($"Stock insuficiente para producto {item.ProductId}");

                    stock.Quantity -= item.Quantity;
                }

                // Obtener usuario y sesión de caja actual
                var user = await _context.Users.FirstAsync();
                var cashSession = await _context.CashSessions
                    .OrderByDescending(c => c.Id)
                    .FirstAsync();

                // Calcular totales
                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal discountTotal = dto.Items.Sum(i => i.Discount);
                decimal total = subtotal - discountTotal;

                // Crear movimiento de caja
                var cashMovement = new CashMovement
                {
                    CashSessionId = cashSession.Id,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    CreationDate = DateTime.UtcNow
                };
                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                // Crear venta
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

                // Crear items de venta
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

                // Crear pagos si se proporcionaron
                if (dto.Payments != null && dto.Payments.Any())
                {
                    foreach (var payment in dto.Payments)
                    {
                        // Convertir el método de pago de manera segura
                        PaymentMethod method;
                        if (Enum.TryParse(payment.Method, true, out method))
                        {
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
            if (sale == null) return false;

            sale.IsDeleted = true;
            await _context.SaveChangesAsync();
            return true;
        }

        private static SaleDto MapSaleToDto(Sale s)
        {
            var items = s.SalesItems?.Select(i => new SaleItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.Product != null ? (i.Product.Name ?? "") : string.Empty,
                UnitPrice = i.UnitPrice,
                Quantity = (int)i.Quantity,
                Discount = i.Discount,
                Total = (i.UnitPrice * i.Quantity) - i.Discount
            }).ToList() ?? new List<SaleItemDto>();

            var payments = s.SalesPayments?.Select(p => new SalePaymentDto
            {
                MethodName = p.Method.ToString(),
                Amount = p.Amount,
                Reference = p.Reference
            }).ToList() ?? new List<SalePaymentDto>();

            return new SaleDto
            {
                Id = s.Id,
                SoldAt = s.SoldAt,
                SellerName = s.SellerUser != null ? (s.SellerUser.UserName ?? "E-commerce") : "E-commerce",
                Subtotal = s.Subtotal,
                DiscountTotal = s.DiscountTotal,
                Total = s.Total,

                CustomerName = string.IsNullOrWhiteSpace(s.CustomerName)
                                ? (s.Client != null ? (s.Client.FullName ?? "Cliente") : (s.DeliveryAddress ?? "Cliente"))
                                : s.CustomerName,

                ClientId = s.ClientId,
                ClientName = s.Client != null ? s.Client.FullName : null,

                DeliveryType = s.DeliveryType,
                DeliveryAddress = s.DeliveryAddress,
                DeliveryCost = s.DeliveryCost,
                DeliveryNote = s.DeliveryNote,

                FulfillmentMethod = s.FulfillmentMethod,
                PaymentStatus = s.PaymentStatus,
                PaymentMethod = s.PaymentMethod,

                Items = items,
                Payments = payments
            };
        }
    }
}