using System;
using System.Collections.Generic;
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

        // TODO: ajustar estos IDs según tu base
        private const int OnlineBranchId = 1;      // sucursal principal / online
        private const int OnlineSellerUserId = 1;  // usuario "online" para ventas web

        public CheckoutService(ForrajeriaContext context)
        {
            _context = context;
        }

        public async Task<CheckoutResponseDto> ProcessCheckoutAsync(CheckoutRequestDto request)
        {
            if (request.Items == null || request.Items.Count == 0)
                throw new Exception("No se enviaron productos en el carrito.");

            if (request.Payments == null || request.Payments.Count == 0)
                throw new Exception("Debe haber al menos un método de pago.");

            // Validar sumatoria de pagos
            var totalPagos = request.Payments.Sum(p => p.Amount);
            if (totalPagos != request.Total)
                throw new Exception("La suma de los pagos no coincide con el total enviado.");

            // Buscar sesión de caja abierta de la sucursal principal
            var cashSession = await _context.CashSessions
                .Where(c => c.BranchId == OnlineBranchId && !c.IsClosed)
                .OrderByDescending(c => c.OpenedAt)
                .FirstOrDefaultAsync();

            if (cashSession == null)
                throw new Exception("No hay una sesión de caja abierta para la sucursal principal.");

            // Determinar cliente (opcional)
            int? clientId = null;

            if (request.Client != null && !string.IsNullOrWhiteSpace(request.Client.Document))
            {
                var existingClient = await _context.Clients
                    .FirstOrDefaultAsync(c => c.Document == request.Client.Document && !c.IsDeleted);

                if (existingClient != null)
                {
                    clientId = existingClient.Id;
                    // actualizar datos básicos
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

            // Cargar productos y validar stock
            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();

            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted && p.IsActived)
                .ToListAsync();

            if (products.Count != productIds.Count)
                throw new Exception("Uno o más productos no existen o están inactivos.");

            // stock por sucursal
            var stocks = await _context.ProductsStocks
                .Where(s => productIds.Contains(s.ProductId) && s.BranchId == OnlineBranchId)
                .ToListAsync();

            var subtotal = 0m;
            var discountTotal = 0m; // por ahora 0, puedes expandir luego

            // Validar stock y calcular subtotal
            foreach (var item in request.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);

                var stockEntry = stocks.FirstOrDefault(s => s.ProductId == item.ProductId);
                var stockDisponible = stockEntry?.Quantity ?? 0;

                if (item.Quantity <= 0)
                    throw new Exception($"Cantidad inválida para el producto ID {item.ProductId}.");

                if (stockDisponible < item.Quantity)
                    throw new Exception($"Stock insuficiente para el producto {product.Name}.");

                subtotal += product.RetailPrice * item.Quantity;
            }

            var totalCalculado = subtotal - discountTotal;

            // Podés permitir que request.Total incluya recargos
            if (request.Total < totalCalculado)
                throw new Exception("El total enviado es menor al calculado por el sistema.");

            // INICIO TRANSACCIÓN
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Crear movimiento de caja
                var cashMovement = new CashMovement
                {
                    CashSessionId = cashSession.Id,
                    Type = 1, // TODO: ajusta según tu enum interno
                    Amount = request.Total,
                    Description = "Venta online",
                    CreationDate = DateTime.UtcNow,
                    TypeOfSale = "ONLINE",
                    MovementCancelled = false
                };
                _context.CashMovements.Add(cashMovement);
                await _context.SaveChangesAsync();

                // Crear venta
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

                if (clientId.HasValue)
                {
                    // si en tu modelo Sale existe ClientId, agregarlo aquí
                    // sale.ClientId = clientId.Value;
                }

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Crear items y actualizar stock
                foreach (var item in request.Items)
                {
                    var product = products.First(p => p.Id == item.ProductId);
                    var stockEntry = stocks.First(s => s.ProductId == item.ProductId);

                    var saleItem = new SaleItem
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
                    };

                    _context.SalesItems.Add(saleItem);

                    stockEntry.Quantity -= item.Quantity;
                }

                await _context.SaveChangesAsync();

                // Registrar pagos
                foreach (var p in request.Payments)
                {
                    var payment = new SalePayment
                    {
                        SaleId = sale.Id,
                        Method = p.Method,
                        Amount = p.Amount,
                        Reference = p.Reference,
                        CreationDate = DateTime.UtcNow,
                        SurchargeAmount = 0 // puedes calcular recargos según método/cuotas
                    };
                    _context.SalesPayments.Add(payment);
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                var stockActualizado = stocks.Select(s => new CheckoutStockDto
                {
                    ProductId = s.ProductId,
                    Stock = s.Quantity
                }).ToList();

                return new CheckoutResponseDto
                {
                    SaleId = sale.Id,
                    Message = "Venta registrada correctamente.",
                    Subtotal = subtotal,
                    DiscountTotal = discountTotal,
                    Total = sale.Total,
                    SoldAt = sale.SoldAt,
                    StockActualizado = stockActualizado,
                    TicketUrl = null // TODO: aquí luego puedes generar y devolver un PDF
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
