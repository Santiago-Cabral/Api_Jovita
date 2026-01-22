// using System.Threading;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.DTOs.Payway;

namespace ForrajeriaJovitaAPI.Services.Interfaces
{
    public interface IPaywayService
    {
        Task<CreateCheckoutResponse> CreateCheckoutAsync(
            CreateCheckoutRequest request,
            CancellationToken cancellationToken = default);
    }
}