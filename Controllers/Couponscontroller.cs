using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ForrajeriaJovitaAPI.DTOs.Coupons;
using ForrajeriaJovitaAPI.Services;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous] // mismo patrón que ProductsController: el front manda el Bearer token igual
    public class CouponsController : ControllerBase
    {
        private readonly ICouponService _couponService;

        public CouponsController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        // GET: api/Coupons  (panel admin)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var coupons = await _couponService.GetAllAsync();
            return Ok(coupons);
        }

        // GET: api/Coupons/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var coupon = await _couponService.GetByIdAsync(id);
            if (coupon == null) return NotFound();
            return Ok(coupon);
        }

        // POST: api/Coupons  (panel admin)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CouponCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest(new { message = "El código es obligatorio" });

            if (dto.Value <= 0)
                return BadRequest(new { message = "El valor del descuento debe ser mayor a 0" });

            if (dto.Type == 0 && dto.Value > 100)
                return BadRequest(new { message = "Un descuento porcentual no puede superar 100" });

            try
            {
                var created = await _couponService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT: api/Coupons/5  (panel admin)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CouponUpdateDto dto)
        {
            if (id != dto.Id)
                return BadRequest(new { message = "El Id de la URL no coincide con el del body" });

            try
            {
                var updated = await _couponService.UpdateAsync(dto);
                if (!updated) return NotFound();
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // DELETE: api/Coupons/5  (panel admin)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _couponService.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }

        // POST: api/Coupons/validate  (checkout público, sin token)
        [HttpPost("validate")]
        public async Task<IActionResult> Validate([FromBody] ValidateCouponRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest(new ValidateCouponResponseDto
                {
                    Valid = false,
                    Message = "Ingresá un código de cupón"
                });

            var result = await _couponService.ValidateAsync(dto.Code, dto.CartTotal);
            return Ok(result);
        }
    }
}