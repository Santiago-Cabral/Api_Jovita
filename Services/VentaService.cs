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
        // GET ALL SALES - Proyección a DTO (evita materialización problemáticas)
        // ============================================================
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

            // Proyección directa a DTOs
            var projected = await query
                .OrderByDescending(s => s.SoldAt)
                .Select(s => new SaleDto
                {
                    Id = s.Id,
                    SoldAt = s.SoldAt,
                    SellerName = s.SellerUser != null ? s.SellerUser.Name + " " + s.SellerUser.LastName : "Web",
                    Subtotal = s.Subtotal,
                    DiscountTotal = s.DiscountTotal,
                    Total = s.Total,
                    DeliveryType = s.DeliveryType,
                    DeliveryAddress = s.DeliveryAddress,
                    DeliveryCost = s.DeliveryCost,
                    DeliveryNote = s.DeliveryNote,
                    // CONVERSIÓN EXPLÍCITA: la entidad usa decimal, el DTO int
                    PaymentStatus = (int)s.PaymentStatus,
                    Items = s.SalesItems.Select(i => new SaleItemDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.Product != null ? i.Product.Name : "Producto",
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
                })
                .ToListAsync();

            return projected;
        }

        // ============================================================
        // GET SALE BY ID (proyección)
        // ============================================================
        public async Task<SaleDto?> GetSaleByIdAsync(int id)
        {
            var dto = await _context.Sales
                .Where(s => s.Id == id)
                .Select(s => new SaleDto
                {
                    Id = s.Id,
                    SoldAt = s.SoldAt,
                    SellerName = s.SellerUser != null ? s.SellerUser.Name + " " + s.SellerUser.LastName : "Web",
                    Subtotal = s.Subtotal,
                    DiscountTotal = s.DiscountTotal,
                    Total = s.Total,
                    DeliveryType = s.DeliveryType,
                    DeliveryAddress = s.DeliveryAddress,
                    DeliveryCost = s.DeliveryCost,
                    DeliveryNote = s.DeliveryNote,
                    PaymentStatus = (int)s.PaymentStatus,
                    Items = s.SalesItems.Select(i => new SaleItemDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.Product != null ? i.Product.Name : "Producto",
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
                })
                .FirstOrDefaultAsync();

            return dto;
        }

        // ============================================================
        // CREATE PUBLIC SALE (WEB / CARRITO)
        // ============================================================
        public async Task<SaleDto> CreatePublicSaleAsync(CreatePublicSaleDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1) Validaciones básicas del DTO
                if (dto.Items == null || !dto.Items.Any())
                    throw new InvalidOperationException("El pedido debe incluir productos.");

                foreach (var it in dto.Items)
                {
                    if (it.Quantity <= 0)
                        throw new InvalidOperationException($"Quantity inválida para producto {it.ProductId}.");
                    if (it.UnitPrice < 0)
                        throw new InvalidOperationException($"UnitPrice inválido para producto {it.ProductId}.");
                }

                // 2) Calcular subtotal/total a partir de los items
                decimal subtotal = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal discountTotal = 0m; // DTO actual no trae descuentos por item
                decimal total = subtotal + dto.ShippingCost - discountTotal;
                if (total < 0) total = 0m;

                // 3) Determinar usuario sistema (seller) — preferir admin@jovita.com, fallback role 1
                var systemUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == "admin@jovita.com" || u.UserName == "admin@jovita.com");

                if (systemUser == null)
                {
                    systemUser = await _context.Users.FirstOrDefaultAsync(u => u.RoleId == 1);
                }

                if (systemUser == null)
                    throw new InvalidOperationException("No se encontró usuario sistema (admin). Crear usuario 'admin@jovita.com' o definir un usuario con RoleId = 1.");

                // 4) Buscar una CashSession para asociar el movimiento de caja (usar la última)
                var activeSession = await _context.CashSessions
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                if (activeSession == null)
                    throw new InvalidOperationException("No se encontró CashSession. Crear/activar una sesión de caja antes de procesar ventas web.");

                // 5) Crear CashMovement completo (no debe quedar con campos NULL)
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
                await _context.SaveChangesAsync(); // obtener cashMovement.Id

                // 6) Crear la entidad Sale asociada al CashMovement y al usuario sistema
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
                    // asignamos decimal (tipo de la entidad)
                    PaymentStatus = 0m, // pendiente por defecto como decimal
                    CreationDate = DateTime.Now
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync(); // obtener sale.Id

                // 7) Agregar items (verificar existencia de producto)
                foreach (var item in dto.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                        throw new InvalidOperationException($"Producto {item.ProductId} no encontrado.");

                    var unitPrice = item.UnitPrice;

                    _context.SalesItems.Add(new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice,
                        Discount = 0,
                        ConversionToBase = 1,
                        DeductedBaseQuantity = item.Quantity,
                        CreationDate = DateTime.Now
                    });
                }

                // 8) Registrar pago(s) — DTO público trae solo PaymentMethod/PaymentReference
                decimal paymentsSum = 0m;
                PaymentMethod paymentMethod;
                if (string.IsNullOrWhiteSpace(dto.PaymentMethod))
                    paymentMethod = PaymentMethod.Cash;
                else
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

                // 9) Guardar items y pagos
                await _context.SaveChangesAsync();

                // 10) Actualizar PaymentStatus en la venta según pagos (usar decimal)
                if (paymentsSum >= total && total > 0)
                    sale.PaymentStatus = 1m; // pagado
                else if (paymentsSum > 0 && paymentsSum < total)
                    sale.PaymentStatus = 2m; // parcial
                else
                    sale.PaymentStatus = 0m; // pendiente

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 11) Retornar DTO completo (usamos la proyección GetSaleByIdAsync)
                return (await GetSaleByIdAsync(sale.Id))!;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        // CREATE SALE (CAJA / INTERNA) - mantiene lógica previa
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
                    PaymentStatus = 1m, // decimal literal
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
                // CONVERSIÓN EXPLÍCITA: el entity usa decimal
                sale.PaymentStatus = Convert.ToDecimal(dto.PaymentStatus.Value);
            }

            await _context.SaveChangesAsync();
            return MapSaleToDto(sale);
        }

        // ============================================================
        // UPDATE ONLY SALE STATUS (partial update safe)
        // ============================================================
        public async Task<SaleDto?> UpdateSaleStatusAsync(int id, int status)
        {
            // Attach a lightweight entity and mark only PaymentStatus as modified.
            var sale = new Sale { Id = id };
            _context.Sales.Attach(sale);

            // set value and mark modified (convert int -> decimal explícito)
            sale.PaymentStatus = Convert.ToDecimal(status);
            _context.Entry(sale).Property(s => s.PaymentStatus).IsModified = true;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Rethrow preserving the original exception so controller can log/return it.
                throw;
            }

            // Return fresh DTO using your projection method
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
        // MAP ENTITY -> DTO (utilizado por UpdateSaleAsync u otros casos)
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
                // convertir decimal -> int para DTO
                PaymentStatus = Convert.ToInt32(s.PaymentStatus),

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
