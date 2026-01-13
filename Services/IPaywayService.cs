using System.Threading;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.DTOs.Payway;

namespace ForrajeriaJovitaAPI.Services
{
    /// <summary>
    /// Servicio para integración con Payway (Decidir)
    /// </summary>
    public interface IPaywayService
    {
        /// <summary>
        /// Crea un checkout de pago con Payway
        /// </summary>
        Task<CheckoutResponse> CreateCheckoutAsync(
            int saleId,
            decimal amount,
            string description,
            string customerName,
            string customerEmail,
            string customerPhone,
            string returnUrl,
            string cancelUrl,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Consulta el estado de un pago por su ID de transacción
        /// </summary>
        Task<PaymentStatusResponse?> GetPaymentStatusAsync(
            string transactionId,
            CancellationToken cancellationToken = default);
    }
}