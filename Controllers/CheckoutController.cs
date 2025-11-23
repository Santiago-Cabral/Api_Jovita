using System;
using System.Threading.Tasks;
using ForrajeriaJovitaAPI.DTOs.Checkout;
using ForrajeriaJovitaAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CheckoutController : ControllerBase
    {
        private readonly ICheckoutService _checkoutService;

        public CheckoutController(ICheckoutService checkoutService)
        {
            _checkoutService = checkoutService;
        }

        [HttpPost]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto request)
        {
            try
            {
                var result = await _checkoutService.ProcessCheckoutAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // podés mejorar el manejo de errores según necesidad
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
