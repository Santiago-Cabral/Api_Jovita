using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MercadoPago.Client.Preference;
using MercadoPago.Client.Payment;
using MercadoPago.Resource.Preference;
using MercadoPago.Resource.Payment;
using ForrajeriaJovitaAPI.DTOs.MercadoPago;
using ForrajeriaJovitaAPI.Services.Interfaces;

namespace ForrajeriaJovitaAPI.Services
{
    public class MercadoPagoService : IMercadoPagoService
    {
        private readonly ILogger<MercadoPagoService> _logger;
        private readonly IConfiguration _configuration;

        public MercadoPagoService(ILogger<MercadoPagoService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<CreateCheckoutResponse> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("💳 [MERCADOPAGO] Creando preferencia - Sale:{Sale} Amount:{Amount}", request.SaleId, request.Amount);

            var transactionId = $"JOV_{DateTime.UtcNow:yyyyMMddHHmmss}_{request.SaleId}_{new Random().Next(1000, 9999)}";

            // FIX #1: la clave anidada es "Frontend:Url", no "FrontendUrl"
            var frontBaseUrl = _configuration["Frontend:Url"] ?? "https://jovita.store";
            var apiBaseUrl = _configuration["ApiPublicUrl"] ?? "https://forrajeria-jovita-api.onrender.com";

            var preferenceRequest = new PreferenceRequest
            {
                Items = new List<PreferenceItemRequest>
                {
                    new PreferenceItemRequest
                    {
                        Title = request.Description ?? $"Pedido #{request.SaleId}",
                        Quantity = 1,
                        CurrencyId = "ARS",
                        UnitPrice = request.Amount
                    }
                },
                Payer = new PreferencePayerRequest
                {
                    Name = request.Customer?.Name,
                    Email = request.Customer?.Email
                },
                ExternalReference = transactionId,
                NotificationUrl = $"{apiBaseUrl}/api/MercadoPago/webhook",
                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = $"{frontBaseUrl}/payment/success",
                    Pending = $"{frontBaseUrl}/payment/pending",
                    Failure = $"{frontBaseUrl}/payment/cancel"
                },
                AutoReturn = "approved"
            };

            var client = new PreferenceClient();
            Preference preference;

            try
            {
                preference = await client.CreateAsync(preferenceRequest, null, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [MERCADOPAGO] Error creando preferencia");
                throw new InvalidOperationException("Error al crear el checkout de pago con Mercado Pago");
            }

            if (preference == null || string.IsNullOrEmpty(preference.Id) || string.IsNullOrEmpty(preference.InitPoint))
            {
                _logger.LogError("❌ [MERCADOPAGO] Respuesta inválida del SDK");
                throw new InvalidOperationException("Respuesta inválida de Mercado Pago");
            }

            // FIX #3: con credenciales de test (Access Token que arranca con "TEST-")
            // hay que usar SandboxInitPoint, sino el checkout no acepta tarjetas de prueba.
            var accessToken = _configuration["MercadoPago:AccessToken"] ?? "";
            var isTestCredential = accessToken.StartsWith("TEST-");
            var checkoutUrl = isTestCredential ? preference.SandboxInitPoint : preference.InitPoint;

            _logger.LogInformation("✅ [MERCADOPAGO] Preferencia OK - Id: {Id} - Modo: {Modo}",
                preference.Id, isTestCredential ? "SANDBOX" : "PRODUCCION");

            return new CreateCheckoutResponse
            {
                CheckoutId = preference.Id,
                CheckoutUrl = checkoutUrl,
                TransactionId = transactionId
            };
        }

        public async Task<MercadoPagoPaymentResponse?> GetPaymentAsync(string paymentId, CancellationToken cancellationToken = default)
        {
            try
            {
                var client = new PaymentClient();
                Payment payment = await client.GetAsync(long.Parse(paymentId), cancellationToken);

                if (payment == null) return null;

                return new MercadoPagoPaymentResponse
                {
                    Id = payment.Id ?? 0,
                    Status = payment.Status,
                    StatusDetail = payment.StatusDetail,
                    TransactionAmount = payment.TransactionAmount ?? 0,
                    ExternalReference = payment.ExternalReference
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ [MERCADOPAGO] No se pudo obtener el pago {PaymentId}", paymentId);
                return null;
            }
        }
    }
}