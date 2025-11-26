using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services;

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

        // ============================================================
        // GET: Todas las ventas (con filtros opcionales)
        // ============================================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetSales(
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
                return StatusCode(500, new { message = "Error al obtener ventas", error = ex.Message });
            }
        }

        // ============================================================
        // GET: Venta por ID
        // ============================================================
        [HttpGet("{id}")]
        public async Task<ActionResult<SaleDto>> GetSale(int id)
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
                _logger.LogError(ex, "Error al obtener venta");
                return StatusCode(500, new { message = "Error al obtener venta", error = ex.Message });
            }
        }

        // ============================================================
        // POST: Crear venta
        // ============================================================
        [HttpPost]
        public async Task<ActionResult<SaleDto>> CreateSale(CreateSaleDto dto)
        {
            try
            {
                if (dto.Items == null || !dto.Items.Any())
                    return BadRequest(new { message = "La venta debe tener al menos un producto" });

                if (dto.Payments == null || !dto.Payments.Any())
                    return BadRequest(new { message = "La venta debe tener al menos un pago" });

                var sale = await _ventaService.CreateSaleAsync(dto);

                return CreatedAtAction(nameof(GetSale), new { id = sale.Id }, sale);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear venta");
                return StatusCode(500, new { message = "Error al crear venta", error = ex.Message });
            }
        }

        // ============================================================
        // PUT: Actualizar venta (Estado, Nota)
        // ============================================================
        [HttpPut("{id}")]
        public async Task<ActionResult<SaleDto>> UpdateSale(int id, UpdateSaleDto dto)
        {
            try
            {
                if (id != dto.Id)
                    return BadRequest(new { message = "El ID no coincide con el body." });

                var updated = await _ventaService.UpdateSaleAsync(dto);

                if (updated == null)
                    return NotFound(new { message = "Venta no encontrada" });

                return Ok(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar venta");
                return StatusCode(500, new { message = "Error al actualizar venta", error = ex.Message });
            }
        }

        // ============================================================
        // GET: Resumen de ventas del día
        // ============================================================
        [HttpGet("today")]
        public async Task<ActionResult<object>> GetTodaySales()
        {
            try
            {
                var summary = await _ventaService.GetTodaySalesSummaryAsync();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen diario");
                return StatusCode(500, new { message = "Error al obtener resumen", error = ex.Message });
            }
        }

        // ============================================================
        // GET: Ventas por período (YYYY/MM)
        // ============================================================
        [HttpGet("period/{year}/{month}")]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetSalesByPeriod(int year, int month)
        {
            try
            {
                if (month < 1 || month > 12)
                    return BadRequest(new { message = "Mes inválido" });

                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1).AddDays(-1);

                var sales = await _ventaService.GetAllSalesAsync(start, end, null);

                return Ok(sales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ventas por periodo");
                return StatusCode(500, new { message = "Error al obtener ventas", error = ex.Message });
            }
        }

        // ============================================================
        // GET: Ventas por vendedor
        // ============================================================
        [HttpGet("seller/{sellerId}")]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetSalesBySeller(
            int sellerId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var sales = await _ventaService.GetAllSalesAsync(startDate, endDate, sellerId);
                return Ok(sales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ventas por vendedor");
                return StatusCode(500, new { message = "Error", error = ex.Message });
            }
        }

        // ============================================================
        // GET: Estadísticas de ventas
        // ============================================================
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetSalesStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Establecer valores por defecto si no vienen en query
                if (!startDate.HasValue)
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                if (!endDate.HasValue)
                    endDate = DateTime.Now;

                if (startDate > endDate)
                    return BadRequest(new { message = "Fecha de inicio inválida" });

                var sales = (await _ventaService.GetAllSalesAsync(startDate, endDate, null)).ToList();

                var result = new
                {
                    Period = new { startDate, endDate },
                    TotalSales = sales.Count,
                    TotalAmount = sales.Sum(s => s.Total),
                    TotalDiscount = sales.Sum(s => s.DiscountTotal),
                    AvgTicket = sales.Any() ? sales.Average(s => s.Total) : 0,
                    SalesByDay = sales
                        .GroupBy(s => s.SoldAt.Date)
                        .Select(g => new { Date = g.Key, Total = g.Sum(x => x.Total) })
                        .OrderBy(x => x.Date)
                        .ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas");
                return StatusCode(500, new { message = "Error", error = ex.Message });
            }
        }

        // ============================================================
        // GET: Total entre fechas
        // ============================================================
        [HttpGet("total")]
        public async Task<ActionResult<object>> GetSalesTotal(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                var sales = (await _ventaService.GetAllSalesAsync(startDate, endDate, null)).ToList();

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
                return StatusCode(500, new { message = "Error", error = ex.Message });
            }
        }
    }
}
