using System;
using System.Linq;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ForrajeriaJovitaAPI.DTOs.Checkout;
using ForrajeriaJovitaAPI.DTOs.Payway;
namespace ForrajeriaJovitaAPI.Services
{
    public class CheckoutService : ICheckoutService
    {
        private readonly ForrajeriaContext _context;
        private readonly ILogger<CheckoutService> _logger;
        private readonly IConfiguration _config;
        private readonly IPaywayService _paywayService;

        private const int OnlineBranchId = 1;
        private const int OnlineSellerUserId = 1;

        public CheckoutService(
            ForrajeriaContext context,
            ILogger<CheckoutService> logger,
            IConfiguration config,
            IPaywayService paywayService)
        {
            _context = context;
            _logger = logger;
            _config = config;
            _paywayService = paywayService;
        }

        public async Task<CheckoutResponseDto> ProcessCheckoutAsync(CheckoutRequestDto request)
        {
            if (request.Items == null || request.Items.Count == 0)
                throw new ArgumentException("No se enviaron productos.");

            if (request.Payments == null || request.Payments.Count == 0)
                throw new ArgumentException("Debe haber al menos un pago.");

            var totalPagos = request.Payments.Sum(p => p.Amount);
            if (totalPagos != request.Total)
                throw new ArgumentException("La suma de los pagos no coincide con el total.");

            var cashSession = await _context.CashSessions
                .Where(c => c.BranchId == OnlineBranchId && !c.IsClosed)
                .OrderByDescending(c => c.OpenedAt)
                .FirstOrDefaultAsync();

            if (cashSession == null)
                throw new InvalidOperationException("No hay sesión de caja abierta.");

            int? clientId = null;
            if (request.Client != null && !string.IsNullOrWhiteSpace(request.Client.Document))
            {
                var existingClient = await _context.Clients
                    .FirstOrDefaultAsync(c => c.Document == request.Client.Document && !c.IsDeleted);

                if (existingClient != null)
                {
                    clientId = existingClient.Id;
                    existingClient.FullName = request.Client.FullName ?? existingClient.FullName;
                    existingClient.Phone = request.Client.Phone ?? existingClient.Phone;
                    _context.Clients.Update(existingClient);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    var newClient = new Client
                    {
                        FullName = request.Client.FullName ?? "Cliente",
                        Phone = request.Client.Phone ?? "",
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

            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();

            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted && p.IsActived)
                .ToListAsync();

            if (products.Count != productIds.Count)
                throw new InvalidOperationException("Un producto no existe o está inactivo.");

            var stocks = await _context.ProductsStocks
                .Where(s => productIds.Contains(s.ProductId) && s.BranchId == OnlineBranchId)
                .ToListAsync();

            decimal subtotal = 0m;
            decimal discountTotal = 0m;

            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0)
                    throw new InvalidOperationException($"Cantidad inválida en producto ID {item.ProductId}");

                if (Math.Abs(item.Quantity - Math.Truncate(item.Quantity)) > 0)
                    throw new InvalidOperationException($"Cantidad no entera para producto ID {item.ProductId}. Debe ser un número entero.");

                var qty = (int)Math.Truncate(item.Quantity);

                var product = products.First(p => p.Id == item.ProductId);
                var stockEntry = stocks.FirstOrDefault(s => s.ProductId == item.ProductId);

                var stockDisponible = stockEntry?.Quantity ?? 0;

                if (stockDisponible < qty)
                    throw new InvalidOperationException($"Stock insuficiente: {product.Name}");

                subtotal += product.RetailPrice * qty;
            }

            var totalCalculado = subtotal - discountTotal;
            if (request.Total < totalCalculado)
                throw new InvalidOperationException("El total enviado es menor al calculado.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            Sale sale;
            try
            {
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

                sale = new Sale
                {
                    CashMovementId = cashMovement.Id,
                    SoldAt = DateTime.UtcNow,
                    SellerUserId = OnlineSellerUserId,
                    Subtotal = subtotal,
                    DiscountTotal = discountTotal,
                    Total = request.Total,
                    PaymentStatus = 0,
                    CreationDate = DateTime.UtcNow
                };

                if (clientId.HasValue)
                    sale.ClientId = clientId.Value;

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in request.Items)
                {
                    var product = products.First(p => p.Id == item.ProductId);
                    var stockEntry = stocks.First(s => s.ProductId == item.ProductId);

                    var qty = (int)Math.Truncate(item.Quantity);

                    _context.SalesItems.Add(new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = product.Id,
                        Quantity = qty,
                        UnitPrice = product.RetailPrice,
                        Discount = 0,
                        BranchName = null,
                        CreationDate = DateTime.UtcNow,
                        ConversionToBase = 1,
                        DeductedBaseQuantity = qty,
                        ProductUnitId = null
                    });

                    stockEntry.Quantity -= qty;
                    _context.ProductsStocks.Update(stockEntry);
                }

                await _context.SaveChangesAsync();

                foreach (var p in request.Payments)
                {
                    _context.SalesPayments.Add(new SalePayment
                    {
                        SaleId = sale.Id,
                        Method = (PaymentMethod)p.Method,
                        Amount = p.Amount,
                        Reference = p.Reference ?? "",
                        CreationDate = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            string? paywayRedirectUrl = null;
            try
            {
                var customerEmail = "cliente@temp.com";
                var customerName = "Cliente";
                var customerPhone = "";

                if (clientId.HasValue)
                {
                    var client = await _context.Clients.FindAsync(clientId.Value);
                    if (client != null)
                    {
                        customerName = client.FullName ?? "Cliente";
                        customerPhone = client.Phone ?? "";
                    }
                }

                var frontendUrl = _config["Frontend:Url"] ?? "https://forrajeria-jovita.vercel.app";

                var paywayResult = await _paywayService.CreatePaymentAsync(new PaywayCheckoutRequest
                {
                    SaleId = sale.Id,
                    Amount = sale.Total,
                    Description = $"Pedido #{sale.Id} - Forrajería Jovita",
                    Customer = new CustomerInfo
                    {
                        Name = customerName,
                        Email = customerEmail,
                        Phone = customerPhone
                    },
                    ReturnUrl = $"{frontendUrl}/pago-exitoso?saleId={sale.Id}",
                    CancelUrl = $"{frontendUrl}/pago-cancelado?saleId={sale.Id}"
                });

                paywayRedirectUrl = paywayResult.CheckoutUrl;

                try
                {
                    var paymentTransaction = new PaymentTransaction
                    {
                        SaleId = sale.Id,
                        TransactionId = paywayResult.TransactionId,
                        CheckoutId = paywayResult.CheckoutId,
                        Status = "pending",
                        Amount = sale.Total,
                        Currency = "ARS",
                        PaymentMethod = "card",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.PaymentTransactions.Add(paymentTransaction);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ PaymentTransaction guardado para Sale {SaleId}, TransactionId: {TransactionId}",
                        sale.Id, paywayResult.TransactionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo guardar PaymentTransaction para Sale {SaleId} (no crítico).", sale.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando checkout en Payway para Sale {SaleId}", sale.Id);
                throw new InvalidOperationException("Error al crear el checkout en Payway. Intente nuevamente.", ex);
            }

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
                TicketUrl = null,
                PaywayRedirectUrl = paywayRedirectUrl
            };
        }
    }
}