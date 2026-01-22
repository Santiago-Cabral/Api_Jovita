// Services/Interfaces/IPaywayService.cs
using ForrajeriaJovitaAPI.DTOs.Payway;
using System.Threading;
using System.Threading.Tasks;

namespace ForrajeriaJovitaAPI.Services.Interfaces
{
    public interface IPaywayService
    {
        Task<CreateCheckoutResponse> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default);
        Task<PaymentStatusResponse?> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default);
    }
}
