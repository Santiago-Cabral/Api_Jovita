using System.Threading;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.DTOs.MercadoPago;

namespace ForrajeriaJovitaAPI.Services.Interfaces
{
    public interface IMercadoPagoService
    {
        Task<CreateCheckoutResponse> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default);
        Task<MercadoPagoPaymentResponse?> GetPaymentAsync(string paymentId, CancellationToken cancellationToken = default);
    }
}