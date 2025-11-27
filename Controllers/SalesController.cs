using Microsoft.AspNetCore.Mvc;
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

        public SalesController(IVentaService ventaService, ILogger<SalesController> logger)
        {
            _ventaService = ventaService;
            _logger = logger;
        }

        // ==========================================================
        // GET: api/sales
        // ==========================================================
        [HttpGet]
        public async Task<IActionResult> GetSales(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? sellerId = null)
        {
            try
            {
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

        // ==========================================================
        // GET: api/sales/{id}
        // ==========================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSaleById(int id)
        {
            try
            {
                var sale = await _ventaService.GetSaleByIdAsync(id);
                if (sale == null)
                    return NotFound(new { message = "Venta no encontrada" });

                return Ok(sale);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener venta #{id}");
                return StatusCode(500, new { message = "Error interno", error = ex.Message });
            }
        }

        // ==========================================================
        // POST: api/sales
        // ==========================================================
        [HttpPost]
        public async Task<IActionResult> CreateSale([FromBody] CreateSaleDto dto)
        {
            try
            {
                if (dto.Items == null || dto.Items.Count == 0)
                    return BadRequest(new { message = "La venta debe tener productos." });

                if (dto.Payments == null || dto.Payments.Count == 0)
                    return BadRequest(new { message = "La venta debe tener al menos un pago." });

                var sale = await _ventaService.CreateSaleAsync(dto);

                return CreatedAtAction(nameof(GetSaleById), new { id = sale.Id }, sale);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando venta");

                return BadRequest(new
                {
                    message = "Error al crear venta",
                    error = ex.Message
                });
            }
        }

        // ==========================================================
        // GET: api/sales/today
        // ==========================================================
        [HttpGet("today")]
        public async Task<IActionResult> GetTodaySummary()
        {
            try
            {
                var summary = await _ventaService.GetTodaySalesSummaryAsync();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen diario");
                return StatusCode(500, new { message = "Error interno", error = ex.Message });
            }
        }

        // ==========================================================
        // GET: api/sales/period/{year}/{month}
        // ==========================================================
        [HttpGet("period/{year:int}/{month:int}")]
        public async Task<IActionResult> GetSalesByPeriod(int year, int month)
        {
            try
            {
                if (month < 1 || month > 12)
                    return BadRequest(new { message = "Mes inválido" });

                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1).AddDays(-1);

                var list = await _ventaService.GetAllSalesAsync(start, end, null);

                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtrando por período");
                return StatusCode(500, new { message = "Error interno", error = ex.Message });
            }
        }

        // ==========================================================
        // GET: api/sales/seller/{sellerId}
        // ==========================================================
        [HttpGet("seller/{sellerId:int}")]
        public async Task<IActionResult> GetSalesBySeller(
            int sellerId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var list = await _ventaService.GetAllSalesAsync(startDate, endDate, sellerId);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtrando por vendedor");
                return StatusCode(500, new { message = "Error interno", error = ex.Message });
            }
        }

        // ==========================================================
        // GET: api/sales/statistics
        // ==========================================================
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                startDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                endDate ??= DateTime.Now;

                var list = await _ventaService.GetAllSalesAsync(startDate, endDate, null);
                var data = list.ToList();

                var stats = new
                {
                    Period = new { startDate, endDate },
                    TotalSales = data.Count,
                    TotalAmount = data.Sum(x => x.Total),
                    TotalDiscount = data.Sum(x => x.DiscountTotal),
                    Average = data.Any() ? data.Average(x => x.Total) : 0,
                    Daily = data
                        .GroupBy(x => x.SoldAt.Date)
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
                _logger.LogError(ex, "Error obteniendo estadísticas");
                return StatusCode(500, new { message = "Error interno", error = ex.Message });
            }
        }

        // ==========================================================
        // GET: api/sales/total
        // ==========================================================
        [HttpGet("total")]
        public async Task<IActionResult> GetTotalRange(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                var list = await _ventaService.GetAllSalesAsync(startDate, endDate, null);
                var data = list.ToList();

                return Ok(new
                {
                    startDate,
                    endDate,
                    TotalSales = data.Count,
                    TotalAmount = data.Sum(x => x.Total),
                    Subtotal = data.Sum(x => x.Subtotal),
                    Discount = data.Sum(x => x.DiscountTotal)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo total");
                return StatusCode(500, new { message = "Error interno", error = ex.Message });
            }
        }
    }
}
