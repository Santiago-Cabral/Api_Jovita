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

        // Helper: intenta parsear "nombre - telefono" o devolver nombre limpio
        private static (string? name, string? phone) ParseCustomerFromDelivery(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return (null, null);
            var r = raw.Trim();
            var dashIdx = r.IndexOf('-');
            if (dashIdx > 0)
            {
                var left = r.Substring(0, dashIdx).Trim();
                var right = r.Substring(dashIdx + 1).Trim();
                return (string.IsNullOrWhiteSpace(left) ? null : left, string.IsNullOrWhiteSpace(right) ? null : right);
            }
            // si no hay '-', devolver todo como posible name (si no es un número puro)
            if (!System.Text.RegularExpressions.Regex.IsMatch(r, @"^\d+$"))
                return (r, null);
            return (null, r); // si es sólo números, puede ser teléfono
        }

        // Checkout público (sin requerir cliente registrado).
        public async Task<SaleDto> CreatePublicSaleAsync(CreatePublicSaleDto dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Customer))
                    throw new ArgumentException("Customer (dirección) es requerido.");

                // validar y descontar stock
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
                decimal shipping = dto.ShippingCost;
                decimal total = subtotal + shipping;

                var cashMovement = new CashMovement
                {
                    CashSessionId = cashSession.Id,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    CreationDate = DateTime.UtcNow
                };

                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                // Si el usuario pasó Email/Phone, buscar cliente existente o crear uno (mejor mantener)
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

                // si dto.Customer viene con "Nombre - telefono" parsearlo
                var (parsedName, parsedPhone) = ParseCustomerFromDelivery(dto.Customer);

                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SellerUserId = user.Id,
                    SoldAt = DateTime.UtcNow,
                    Subtotal = subtotal,
                    DiscountTotal = 0,
                    // Guardamos nombre/email/phone explícitamente
                    CustomerName = !string.IsNullOrWhiteSpace(parsedName) ? parsedName : dto.Customer,
                    CustomerPhone = string.IsNullOrWhiteSpace(dto.Phone) ? parsedPhone : dto.Phone,
                    CustomerEmail = dto.Email,
                    Total = total,
                    PaymentStatus = 0,
                    CreationDate = DateTime.UtcNow,
                    DeliveryAddress = dto.Customer,
                    DeliveryCost = shipping,
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

                // No procesamos pagos complejos aquí (si los envías en DTO lo integrás)
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

        // Venta desde POS/admin (espera Payments en CreateSaleDto)
        public async Task<SaleDto> CreateSaleAsync(CreateSaleDto dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // validar y descontar stock
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
                var cashSession = await _context.CashSessions
                    .OrderByDescending(c => c.Id)
                    .FirstAsync();

                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal discountTotal = dto.Items.Sum(i => i.Discount);
                decimal total = subtotal - discountTotal;

                var cashMovement = new CashMovement
                {
                    CashSessionId = cashSession.Id,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    CreationDate = DateTime.UtcNow
                };
                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

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

                if (dto.Payments != null && dto.Payments.Any())
                {
                    foreach (var payment in dto.Payments)
                    {
                        if (!Enum.IsDefined(typeof(PaymentMethod), payment.Method))
                            throw new InvalidOperationException($"Método de pago inválido: {payment.Method}");

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

            // permitir actualizar nombre / contacto desde admin (si viene)
            if (!string.IsNullOrWhiteSpace(dto.CustomerName))
                sale.CustomerName = dto.CustomerName;

            if (!string.IsNullOrWhiteSpace(dto.CustomerPhone))
                sale.CustomerPhone = dto.CustomerPhone;

            if (!string.IsNullOrWhiteSpace(dto.CustomerEmail))
                sale.CustomerEmail = dto.CustomerEmail;

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

        // Helper para extraer nombre legible a partir de DeliveryAddress si CustomerName no existe
        private static string? NameFromDeliveryAddress(string? delivery)
        {
            if (string.IsNullOrWhiteSpace(delivery)) return null;
            var (name, phone) = ParseCustomerFromDelivery(delivery);
            if (!string.IsNullOrWhiteSpace(name)) return name;
            // si no hay nombre, si delivery tiene texto y no es solo números, devolverlo truncado
            var trimmed = delivery.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+$"))
            {
                var first = trimmed.Split(new[] { '-', ',' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (first.Length > 0) return first;
            }
            return null;
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

            // CustomerName: preferir campo explícito, luego client, luego parsear deliveryAddress, luego fallback
            var customerName =
                !string.IsNullOrWhiteSpace(s.CustomerName) ? s.CustomerName
                : (s.Client != null && !string.IsNullOrWhiteSpace(s.Client.FullName) ? s.Client.FullName
                : NameFromDeliveryAddress(s.DeliveryAddress)
                );

            if (string.IsNullOrWhiteSpace(customerName))
                customerName = $"Orden #{s.Id}";

            return new SaleDto
            {
                Id = s.Id,
                SoldAt = s.SoldAt,
                SellerName = s.SellerUser != null ? (s.SellerUser.UserName ?? "E-commerce") : "E-commerce",
                Subtotal = s.Subtotal,
                DiscountTotal = s.DiscountTotal,
                Total = s.Total,
                CustomerName = customerName,
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
