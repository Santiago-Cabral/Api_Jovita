using System.Threading;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.DTOs.Payway; // Ensure this namespace exists and contains CreateCheckoutResponse
using ForrajeriaJovitaAPI.Models.DTOs; // <--- Línea clave

namespace ForrajeriaJovitaAPI.Services.Interfaces
{
    public interface IPaywayService
    {
        // Changed return type from CheckoutResponse to CreateCheckoutResponse
        Task<CreateCheckoutResponse> CreateCheckoutAsync(
            CreateCheckoutRequest request,
            CancellationToken cancellationToken = default);

        Task<PaymentStatusResponse?> GetPaymentStatusAsync(
            string transactionId,
            CancellationToken cancellationToken = default);
    }
}