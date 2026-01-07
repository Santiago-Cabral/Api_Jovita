using System.Threading.Tasks;
using ForrajeriaJovitaAPI.DTOs.Payway;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IPaywayService
    {
        Task<PaywayCheckoutResponse> CreatePaymentAsync(PaywayCheckoutRequest request);
    }
}