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

        // GET ALL SALES
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

        // GET SALE BY ID
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

        // CREATE PUBLIC SALE (WEB)
        public async Task<SaleDto> CreatePublicSaleAsync(CreatePublicSaleDto dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                if (string.IsNullOrWhiteSpace(dto.Customer))
                    throw new ArgumentException("Customer (dirección) es requerido.");

                // 1) Check and decrement stock from central branch
                foreach (var item in dto.Items)
                {
                    var stock = await _context.ProductsStocks.FirstOrDefaultAsync(s =>
                        s.ProductId == item.ProductId &&
                        s.BranchId == CASA_CENTRAL_BRANCH_ID);

                    if (stock == null || stock.Quantity < item.Quantity)
                        throw new InvalidOperationException("Stock insuficiente");

                    stock.Quantity -= item.Quantity;
                }

                // 2) Base data
                var user = await _context.Users.FirstAsync();
                var cashSession = await _context.CashSessions
                    .OrderByDescending(c => c.Id)
                    .FirstAsync();

                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal total = subtotal + dto.ShippingCost;

                // 3) Cash movement
                var cashMovement = new CashMovement
                {
                    CashSessionId = cashSession.Id,
                    Type = CashMovementType.Sale,
                    Amount = total,
                    CreationDate = DateTime.UtcNow
                };

                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                // 4) Try to find or create client if email/phone provided
                Client? client = null;
                if (!string.IsNullOrWhiteSpace(dto.Email) || !string.IsNullOrWhiteSpace(dto.Phone))
                {
                    if (!string.IsNullOrWhiteSpace(dto.Email))
                    {
                        client = await _context.Clients
                            .FirstOrDefaultAsync(c => !c.IsDeleted && c.Email == dto.Email);
                    }

                    if (client == null && !string.IsNullOrWhiteSpace(dto.Phone))
                    {
                        client = await _context.Clients
                            .FirstOrDefaultAsync(c => !c.IsDeleted && c.Phone == dto.Phone);
                    }

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

                // 5) Create Sale and associate client + delivery address
                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SellerUserId = user.Id,
                    SoldAt = DateTime.UtcNow,
                    Subtotal = subtotal,
                    DiscountTotal = 0,
                    CustomerName = dto.Customer,        // <-- importante
                    Total = total,
                    PaymentStatus = 0,                  // pendiente por defecto
                    CreationDate = DateTime.UtcNow,
                    DeliveryAddress = dto.Customer,
                    DeliveryCost = dto.ShippingCost,

                    // AGREGAMOS ESTOS CAMPOS para exponerlos al frontend
                    PaymentMethod = dto.PaymentMethod,
                    FulfillmentMethod = dto.FulfillmentMethod
                };

                if (client != null)
                    sale.ClientId = client.Id;

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // 6) Items
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

        // UPDATE SALE
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

        // TODAY SUMMARY
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

        // SOFT DELETE
        public async Task<bool> DeleteSaleAsync(int id)
        {
            var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return false;

            sale.IsDeleted = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // MAPPER
        private static SaleDto MapSaleToDto(Sale s)
        {
            var items = s.SalesItems?.Select(i => new SaleItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.Product != null ? (i.Product.Name ?? "") : string.Empty,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                Discount = i.Discount,
                Total = (i.UnitPrice * i.Quantity) - i.Discount
            }).ToList() ?? new List<SaleItemDto>();

            var payments = s.SalesPayments?.Select(p =>
            {
                string methodName;
                try
                {
                    methodName = p.Method != null ? p.Method.ToString() : p.Reference ?? string.Empty;
                }
                catch
                {
                    methodName = p.Reference ?? string.Empty;
                }

                return new SalePaymentDto
                {
                    MethodName = methodName,
                    Amount = p.Amount,
                    Reference = p.Reference
                };
            }).ToList() ?? new List<SalePaymentDto>();

            return new SaleDto
            {
                Id = s.Id,
                SoldAt = s.SoldAt,
                SellerName = s.SellerUser != null ? (s.SellerUser.UserName ?? "E-commerce") : "E-commerce",
                Subtotal = s.Subtotal,
                DiscountTotal = s.DiscountTotal,
                Total = s.Total,

                // ---- AHORA EXPONEMOS LOS CAMPOS IMPORTANTES ----
                CustomerName = string.IsNullOrWhiteSpace(s.CustomerName)
                                ? (s.Client != null ? (s.Client.FullName ?? "Cliente") : (s.DeliveryAddress ?? "Cliente"))
                                : s.CustomerName,

                ClientId = s.ClientId,
                ClientName = s.Client != null ? s.Client.FullName : null,

                DeliveryType = s.DeliveryType,
                DeliveryAddress = s.DeliveryAddress,
                DeliveryCost = s.DeliveryCost,
                DeliveryNote = s.DeliveryNote,

                FulfillmentMethod = s.FulfillmentMethod,   // e.g. "delivery" | "pickup"
                PaymentStatus = s.PaymentStatus,
                PaymentMethod = s.PaymentMethod,

                Items = items,
                Payments = payments
            };
        }
    }
}
