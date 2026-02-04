using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ForrajeriaJovitaAPI.Services;
using ForrajeriaJovitaAPI.DTOs;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController : ControllerBase
    {
        private readonly IVentaService _ventaService;
        private readonly ILogger<SalesController> _logger;

        public SalesController(
            IVentaService ventaService,
            ILogger<SalesController> logger)
        {
            _ventaService = ventaService;
            _logger = logger;
        }

        // ======================================================================
        // GET /api/sales
        // ======================================================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetSales(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? sellerId = null)
        {
            try
            {
                _logger.LogInformation(
                    "Consultando ventas. start={Start}, end={End}, seller={SellerId}",
                    startDate, endDate, sellerId);

                var sales = await _ventaService.GetAllSalesAsync(startDate, endDate, sellerId);
                return Ok(sales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ventas");
                return StatusCode(500, new
                {
                    message = "Error al obtener ventas",
                    error = ex.Message
                });
            }
        }

        // ======================================================================
        // GET /api/sales/{id}
        // ======================================================================
        [HttpGet("{id:int}")]
        public async Task<ActionResult<SaleDto>> GetSale(int id)
        {
            try
            {
                var sale = await _ventaService.GetSaleByIdAsync(id);

                if (sale == null)
                {
                    _logger.LogWarning("Venta {Id} no encontrada", id);
                    return NotFound(new { message = "Venta no encontrada" });
                }

                return Ok(sale);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener venta {Id}", id);
                return StatusCode(500, new
                {
                    message = "Error al obtener venta",
                    error = ex.Message
                });
            }
        }

        // ======================================================================
        // POST /api/sales  (VENTA INTERNA / CAJA)
        // ======================================================================
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<SaleDto>> CreateSale(
            [FromBody] CreateSaleDto dto)
        {
            try
            {
                if (dto.Items == null || !dto.Items.Any())
                    return BadRequest(new { message = "La venta debe incluir productos." });

                if (dto.Payments == null || !dto.Payments.Any())
                    return BadRequest(new { message = "La venta debe incluir pagos." });

                var sale = await _ventaService.CreateSaleAsync(dto);

                _logger.LogInformation("Venta creada correctamente: {SaleId}", sale.Id);

                return CreatedAtAction(
                    nameof(GetSale),
                    new { id = sale.Id },
                    sale);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al crear venta");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear venta");
                return StatusCode(500, new
                {
                    message = "Error al crear venta",
                    error = ex.Message
                });
            }
        }

        // ======================================================================
        // 🟢 POST /api/sales/public  (VENTA WEB / CARRITO)
        // ======================================================================
        [HttpPost("public")]
        [AllowAnonymous]
        public async Task<ActionResult<SaleDto>> CreatePublicSale(
            [FromBody] CreatePublicSaleDto dto)
        {
            try
            {
                if (dto.Items == null || !dto.Items.Any())
                    return BadRequest(new { message = "El pedido debe incluir productos." });

                var sale = await _ventaService.CreatePublicSaleAsync(dto);

                _logger.LogInformation(
                    "Venta pública creada correctamente: {SaleId}", sale.Id);

                return Ok(sale);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación en venta pública");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear venta pública");
                return StatusCode(500, new
                {
                    message = "Error al crear venta pública",
                    error = ex.Message
                });
            }
        }

        // ======================================================================
        // PUT /api/sales/{id}
        // ======================================================================
        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<ActionResult<SaleDto>> UpdateSale(
            int id,
            [FromBody] UpdateSaleDto dto)
        {
            try
            {
                var updated = await _ventaService.UpdateSaleAsync(id, dto);

                if (updated == null)
                {
                    _logger.LogWarning("Venta {Id} no encontrada al actualizar", id);
                    return NotFound(new { message = "Venta no encontrada" });
                }

                return Ok(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar venta {Id}", id);
                return StatusCode(500, new
                {
                    message = "Error al actualizar venta",
                    error = ex.Message
                });
            }
        }

        // ======================================================================
        // PUT /api/sales/{id}/status
        // ======================================================================
        public class UpdateSaleStatusDto
        {
            public int Status { get; set; } // 0 Pendiente | 1 Pagado | 2 Entregado
        }

        [HttpPut("{id:int}/status")]
        [Authorize]
        public async Task<ActionResult<SaleDto>> UpdateSaleStatus(
            int id,
            [FromBody] UpdateSaleStatusDto dto)
        {
            try
            {
                var updated = await _ventaService.UpdateSaleAsync(
                    id,
                    new UpdateSaleDto
                    {
                        PaymentStatus = dto.Status
                    });

                if (updated == null)
                {
                    _logger.LogWarning(
                        "Venta {Id} no encontrada al actualizar estado", id);
                    return NotFound(new { message = "Venta no encontrada" });
                }

                return Ok(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error al actualizar estado de venta {Id}", id);
                return StatusCode(500, new
                {
                    message = "Error al actualizar estado de la venta",
                    error = ex.Message
                });
            }
        }

        // ======================================================================
        // GET /api/sales/today
        // ======================================================================
        [HttpGet("today")]
        [Authorize]
        public async Task<ActionResult<object>> GetToday()
        {
            try
            {
                var summary = await _ventaService.GetTodaySalesSummaryAsync();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen del día");
                return StatusCode(500, new
                {
                    message = "Error al obtener resumen del día",
                    error = ex.Message
                });
            }
        }

        // ======================================================================
        // GET /api/sales/period/{year}/{month}
        // ======================================================================
        [HttpGet("period/{year:int}/{month:int}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetByPeriod(
            int year,
            int month)
        {
            try
            {
                if (month < 1 || month > 12)
                    return BadRequest(new { message = "Mes inválido (1-12)" });

                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1).AddSeconds(-1);

                var sales = await _ventaService.GetAllSalesAsync(start, end, null);
                return Ok(sales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ventas del período");
                return StatusCode(500, new
                {
                    message = "Error al obtener ventas del período",
                    error = ex.Message
                });
            }
        }

        // ======================================================================
        // GET /api/sales/seller/{sellerId}
        // ======================================================================
        [HttpGet("seller/{sellerId:int}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetBySeller(
            int sellerId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var sales = await _ventaService
                    .GetAllSalesAsync(startDate, endDate, sellerId);

                return Ok(sales);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al obtener ventas del vendedor {SellerId}",
                    sellerId);

                return StatusCode(500, new
                {
                    message = "Error al obtener ventas del vendedor",
                    error = ex.Message
                });
            }
        }

        // ======================================================================
        // GET /api/sales/statistics
        // ======================================================================
        [HttpGet("statistics")]
        [Authorize]
        public async Task<ActionResult<object>> GetStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                startDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                endDate ??= DateTime.Now;

                var sales = (await _ventaService
                        .GetAllSalesAsync(startDate, endDate, null))
                    .ToList();

                var stats = new
                {
                    Period = new { startDate, endDate },
                    TotalSales = sales.Count,
                    TotalAmount = sales.Sum(s => s.Total),
                    TotalDiscounts = sales.Sum(s => s.DiscountTotal),
                    AverageTicket = sales.Any() ? sales.Average(s => s.Total) : 0,
                    SalesByDay = sales
                        .GroupBy(s => s.SoldAt.Date)
                        .Select(g => new
                        {
                            Date = g.Key,
                            Count = g.Count(),
                            Total = g.Sum(x => x.Total)
                        })
                        .OrderBy(x => x.Date)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas");
                return StatusCode(500, new
                {
                    message = "Error al obtener estadísticas",
                    error = ex.Message
                });
            }
        }
        // ======================================================================
        // DELETE /api/sales/{id}  (SOFT DELETE)
        // ======================================================================
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteSale(int id)
        {
            try
            {
                var ok = await _ventaService.DeleteSaleAsync(id);

                if (!ok)
                {
                    _logger.LogWarning("Venta {Id} no encontrada para borrar", id);
                    return NotFound(new { message = "Venta no encontrada" });
                }

                _logger.LogInformation("Venta {Id} marcada como eliminada", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al borrar venta {Id}", id);
                return StatusCode(500, new
                {
                    message = "Error al borrar venta",
                    error = ex.Message
                });
            }
        }


        // ======================================================================
        // GET /api/sales/total
        // ======================================================================
        [HttpGet("total")]
        [Authorize]
        public async Task<ActionResult<object>> GetTotal(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                var sales = (await _ventaService
                        .GetAllSalesAsync(startDate, endDate, null))
                    .ToList();

                var result = new
                {
                    startDate,
                    endDate,
                    TotalSales = sales.Count,
                    TotalAmount = sales.Sum(s => s.Total),
                    TotalSubtotal = sales.Sum(s => s.Subtotal),
                    TotalDiscount = sales.Sum(s => s.DiscountTotal)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener totales");
                return StatusCode(500, new
                {
                    message = "Error al obtener totales",
                    error = ex.Message
                });
            }
        }
    }
}
