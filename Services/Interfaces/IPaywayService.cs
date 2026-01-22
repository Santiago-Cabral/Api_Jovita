using ForrajeriaJovitaAPI.DTOs.Payway;

namespace ForrajeriaJovitaAPI.Services.Interfaces
{
    public interface IPaywayService
    {
        Task<CreateCheckoutResponse> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default);
        Task<PaymentStatusResponse?> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default);
    }
}
}