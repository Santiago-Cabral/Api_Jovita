// Services/IPaywayService.cs
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.DTOs.Payway;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IPaywayService
    {
        /// <summary>
        /// Crea un checkout en Payway Ventas Online (Forms)
        /// </summary>
        /// <param name="request">Datos del pago</param>
        /// <returns>Respuesta con URL de checkout y IDs</returns>
        Task<PaywayCheckoutResponse> CreatePaymentAsync(PaywayCheckoutRequest request);
    }
}