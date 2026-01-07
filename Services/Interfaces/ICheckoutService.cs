using System.Threading.Tasks;
using ForrajeriaJovitaAPI.DTOs.Checkout;

namespace ForrajeriaJovitaAPI.Services.Interfaces
{
    public interface ICheckoutService
    {
        Task<CheckoutResponseDto> ProcessCheckoutAsync(CheckoutRequestDto request);
    }
}