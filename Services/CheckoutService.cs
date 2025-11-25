using System;
using System.Linq;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs.Checkout;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ForrajeriaJovitaAPI.Services
{
    public class CheckoutService : ICheckoutService
    {
        private readonly ForrajeriaContext _context;

        private const int OnlineBranchId = 1;
        private const int OnlineSellerUserId = 1;

        public CheckoutService(ForrajeriaContext context)
        {
            _context = context;
        }

        public async Task<CheckoutResponseDto> ProcessCheckoutAsync(CheckoutRequestDto request)
        {
            if (request.Items == null || request.Items.Count == 0)
                throw new Exception("No se enviaron productos.");

            if (request.Payments == null || request.Payments.Count == 0)
                throw new Exception("Debe haber al menos un pago.");

            var totalPagos = request.Payments.Sum(p => p.Amount);
            if (totalPagos != request.Total)
                throw new Exception("La suma de los pagos no coincide con el total.");

            // Sesión de caja abierta
            var cashSession = await _context.CashSessions
                .Where(c => c.BranchId == OnlineBranchId && !c.IsClosed)
                .OrderByDescending(c => c.OpenedAt)
                .FirstOrDefaultAsync();

            if (cashSession == null)
                throw new Exception("No hay sesión de caja abierta.");

            // CLIENTE
            int? clientId = null;

            if (request.Client != null && !string.IsNullOrWhiteSpace(request.Client.Document))
            {
                var existingClient = await _context.Clients
                    .FirstOrDefaultAsync(c => c.Document == request.Client.Document && !c.IsDeleted);

                if (existingClient != null)
                {
                    clientId = existingClient.Id;
                    existingClient.FullName = request.Client.FullName;
                    existingClient.Phone = request.Client.Phone;
                }
                else
                {
                    var newClient = new Client
                    {
                        FullName = request.Client.FullName,
                        Phone = request.Client.Phone,
                        Document = request.Client.Document,
                        Amount = 0,
                        DebitBalance = 0,
                        IsDeleted = false,
                        CreationDate = DateTime.UtcNow
                    };
                    _context.Clients.Add(newClient);
                    await _context.SaveChangesAsync();
                    clientId = newClient.Id;
                }
            }

            // PRODUCTOS
            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();

            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted && p.IsActived)
                .ToListAsync();

            if (products.Count != productIds.Count)
                throw new Exception("Un producto no existe o está inactivo.");

            // STOCKS
            var stocks = await _context.ProductsStocks
                .Where(s => productIds.Contains(s.ProductId) && s.BranchId == OnlineBranchId)
                .ToListAsync();

            // CÁLCULOS
            var subtotal = 0m;
            var discountTotal = 0m;

            foreach (var item in request.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);
                var stockEntry = stocks.FirstOrDefault(s => s.ProductId == item.ProductId);

                var stockDisponible = stockEntry?.Quantity ?? 0;

                if (item.Quantity <= 0)
                    throw new Exception($"Cantidad inválida en producto ID {item.ProductId}");

                if (stockDisponible < item.Quantity)
                    throw new Exception($"Stock insuficiente: {product.Name}");

                subtotal += product.RetailPrice * item.Quantity;
            }

            var totalCalculado = subtotal - discountTotal;

            if (request.Total < totalCalculado)
                throw new Exception("El total enviado es menor al calculado.");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // MOVIMIENTO DE CAJA
                var cashMovement = new CashMovement
                {
                    CashSessionId = cashSession.Id,
                    Type = CashMovementType.Sale,
                    Amount = request.Total,
                    Description = "Venta online",
                    CreationDate = DateTime.UtcNow
                };

                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                // VENTA
                var sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SoldAt = DateTime.UtcNow,
                    SellerUserId = OnlineSellerUserId,
                    Subtotal = subtotal,
                    DiscountTotal = discountTotal,
                    Total = request.Total,
                    CreationDate = DateTime.UtcNow
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // ITEMS + STOCK
                foreach (var item in request.Items)
                {
                    var product = products.First(p => p.Id == item.ProductId);
                    var stockEntry = stocks.First(s => s.ProductId == item.ProductId);

                    _context.SalesItems.Add(new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = product.RetailPrice,
                        Discount = 0,
                        BranchName = null,
                        CreationDate = DateTime.UtcNow,
                        ConversionToBase = 1,
                        DeductedBaseQuantity = item.Quantity,
                        ProductUnitId = null
                    });

                    stockEntry.Quantity -= item.Quantity;
                }

                await _context.SaveChangesAsync();

                // PAGOS (sin SurchargeAmount)
                foreach (var p in request.Payments)
                {
                    _context.SalesPayments.Add(new SalePayment
                    {
                        SaleId = sale.Id,
                        Method = (PaymentMethod)p.Method,
                        Amount = p.Amount,
                        Reference = p.Reference,
                        CreationDate = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new CheckoutResponseDto
                {
                    SaleId = sale.Id,
                    Message = "Venta registrada correctamente.",
                    Subtotal = subtotal,
                    DiscountTotal = discountTotal,
                    Total = sale.Total,
                    SoldAt = sale.SoldAt,
                    StockActualizado = stocks.Select(s => new CheckoutStockDto
                    {
                        ProductId = s.ProductId,
                        Stock = s.Quantity
                    }).ToList(),
                    TicketUrl = null
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
