using System;
using System.Linq;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ForrajeriaJovitaAPI.DTOs.Checkout; // para CheckoutRequestDto, CheckoutResponseDto, CheckoutStockDto
using ForrajeriaJovitaAPI.DTOs.Payway;   // si usas Payway DTOs aquí también


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

            // Sesión de caja abierta
            var cashSession = await _context.CashSessions
                .Where(c => c.BranchId == OnlineBranchId && !c.IsClosed)
                .OrderByDescending(c => c.OpenedAt)
                .FirstOrDefaultAsync();

            if (cashSession == null)
                throw new InvalidOperationException("No hay sesión de caja abierta.");

            // CLIENTE (opcional)
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
                    _context.Clients.Update(existingClient);
                    await _context.SaveChangesAsync();
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
                throw new InvalidOperationException("Un producto no existe o está inactivo.");

            // STOCKS
            var stocks = await _context.ProductsStocks
                .Where(s => productIds.Contains(s.ProductId) && s.BranchId == OnlineBranchId)
                .ToListAsync();

            // CÁLCULOS
            decimal subtotal = 0m;
            decimal discountTotal = 0m;

            // Validaciones y cálculo del subtotal usando cantidades enteras
            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0)
                    throw new InvalidOperationException($"Cantidad inválida en producto ID {item.ProductId}");

                // Aceptar solo cantidades enteras
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

            // Crear venta + descontar stock + pagos en transacción DB
            using var transaction = await _context.Database.BeginTransactionAsync();
            Sale sale;
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
                sale = new Sale
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
                    sale.ClientId = clientId.Value;

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // ITEMS + STOCK
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
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // ---------- Fuera de la transacción: crear checkout en Payway ----------
            string paywayRedirectUrl = null;
            try
            {
                // descripción y transactionId para Payway
                var description = $"Pedido #{sale.Id} - Forrajeria Jovita";
                var transactionId = sale.Id.ToString();

                var paywayResult = await _paywayService.CreatePaymentAsync(sale.Total, description, transactionId);

                if (string.IsNullOrWhiteSpace(paywayResult))
                {
                    _logger.LogError("Payway no devolvió URL ni body al crear pago para Sale {SaleId}", sale.Id);
                    throw new InvalidOperationException("No se recibió respuesta válida de Payway.");
                }

                // Verificar si lo que devolvió es una URL
                if (Uri.TryCreate(paywayResult, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    paywayRedirectUrl = paywayResult;
                }
                else
                {
                    // Si CreatePaymentAsync devolvió JSON raw en vez de URL, intentamos extraer URL del JSON
                    if (paywayResult.TrimStart().StartsWith("{"))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(paywayResult);
                            var root = doc.RootElement;
                            // intentamos las claves comunes
                            if (root.TryGetProperty("checkout_url", out var c1) && c1.ValueKind == System.Text.Json.JsonValueKind.String)
                                paywayRedirectUrl = c1.GetString();
                            else if (root.TryGetProperty("payment_url", out var c2) && c2.ValueKind == System.Text.Json.JsonValueKind.String)
                                paywayRedirectUrl = c2.GetString();
                            else if (root.TryGetProperty("url", out var c3) && c3.ValueKind == System.Text.Json.JsonValueKind.String)
                                paywayRedirectUrl = c3.GetString();
                        }
                        catch (System.Text.Json.JsonException) { /* ignore */ }
                    }
                }

                if (string.IsNullOrWhiteSpace(paywayRedirectUrl))
                {
                    _logger.LogError("No se pudo extraer una URL válida de la respuesta de Payway para Sale {SaleId}. Raw: {Raw}", sale.Id, paywayResult.Length <= 1000 ? paywayResult : paywayResult.Substring(0, 1000));
                    throw new InvalidOperationException("No se obtuvo URL de checkout desde Payway.");
                }

                // Guardar un registro de PaymentTransaction mínimo (model ya presente en el proyecto)
                try
                {
                    var paymentTransaction = new PaymentTransaction
                    {
                        SaleId = sale.Id,
                        TransactionId = sale.Id.ToString(),
                        CheckoutId = null,
                        Status = "pending",
                        Amount = sale.Total,
                        Currency = "ARS",
                        PaymentMethod = "card",
                        CreatedAt = DateTime.UtcNow,
                        AdditionalData = paywayResult.Length <= 1000 ? paywayResult : paywayResult.Substring(0, 1000)
                    };

                    _context.PaymentTransactions.Add(paymentTransaction);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo guardar PaymentTransaction para Sale {SaleId} (no crítico).", sale.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando checkout en Payway para Sale {SaleId}", sale.Id);
                // Política: lanzamos para que el controller devuelva 500 y el frontend no redirija.
                throw new InvalidOperationException("Error al crear el checkout en Payway. Intente nuevamente.", ex);
            }

            // Devolver DTO con la URL lista para redirigir
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
