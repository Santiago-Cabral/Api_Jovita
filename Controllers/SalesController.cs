using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.DTOs;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.Services;

namespace ForrajeriaJovitaAPI.Controllers
{
    /// <summary>
    /// Controlador para gestión de ventas
    /// </summary>
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

        /// <summary>
        /// Obtiene todas las ventas con filtros opcionales
        /// </summary>
        /// <param name="startDate">Fecha de inicio (opcional)</param>
        /// <param name="endDate">Fecha de fin (opcional)</param>
        /// <param name="sellerId">ID del vendedor (opcional)</param>
        /// <returns>Lista de ventas</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SaleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetSales(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? sellerId = null)
        {
            try
            {
                _logger.LogInformation("Obteniendo ventas - StartDate: {StartDate}, EndDate: {EndDate}, SellerId: {SellerId}",
                    startDate, endDate, sellerId);

                var sales = await _ventaService.GetAllSalesAsync(startDate, endDate, sellerId);

                _logger.LogInformation("Se obtuvieron {Count} ventas", sales.Count());

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

        /// <summary>
        /// Obtiene una venta por su ID
        /// </summary>
        /// <param name="id">ID de la venta</param>
        /// <returns>Detalle de la venta</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(SaleDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SaleDto>> GetSale(int id)
        {
            try
            {
                _logger.LogInformation("Obteniendo venta con ID: {Id}", id);

                var sale = await _ventaService.GetSaleByIdAsync(id);

                if (sale == null)
                {
                    _logger.LogWarning("Venta con ID {Id} no encontrada", id);
                    return NotFound(new { message = "Venta no encontrada" });
                }

                return Ok(sale);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener venta con ID {Id}", id);
                return StatusCode(500, new
                {
                    message = "Error al obtener venta",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Crea una nueva venta
        /// </summary>
        /// <param name="dto">Datos de la venta</param>
        /// <returns>Venta creada</returns>
        [HttpPost]
        [ProducesResponseType(typeof(SaleDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SaleDto>> CreateSale([FromBody] CreateSaleDto dto)
        {
            try
            {
                _logger.LogInformation("Creando nueva venta - Vendedor: {SellerId}, Items: {ItemCount}",
                    dto.SellerUserId, dto.Items.Count);

                // Validaciones básicas
                if (dto.Items == null || !dto.Items.Any())
                {
                    return BadRequest(new { message = "La venta debe tener al menos un producto" });
                }

                if (dto.Payments == null || !dto.Payments.Any())
                {
                    return BadRequest(new { message = "La venta debe tener al menos un método de pago" });
                }

                var sale = await _ventaService.CreateSaleAsync(dto);

                _logger.LogInformation("Venta creada exitosamente con ID: {Id}, Total: {Total}",
                    sale.Id, sale.Total);

                return CreatedAtAction(nameof(GetSale), new { id = sale.Id }, sale);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al crear venta");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear venta");
                return BadRequest(new
                {
                    message = "Error al crear la venta",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene el resumen de ventas del día actual
        /// </summary>
        /// <returns>Resumen de ventas (cantidad, total, descuentos)</returns>
        [HttpGet("today")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetTodaySales()
        {
            try
            {
                _logger.LogInformation("Obteniendo resumen de ventas del día");

                var summary = await _ventaService.GetTodaySalesSummaryAsync();

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen de ventas del día");
                return StatusCode(500, new
                {
                    message = "Error al obtener resumen",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene las ventas de un período específico
        /// </summary>
        /// <param name="year">Año</param>
        /// <param name="month">Mes</param>
        /// <returns>Lista de ventas del mes</returns>
        [HttpGet("period/{year}/{month}")]
        [ProducesResponseType(typeof(IEnumerable<SaleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetSalesByPeriod(int year, int month)
        {
            try
            {
                if (month < 1 || month > 12)
                {
                    return BadRequest(new { message = "El mes debe estar entre 1 y 12" });
                }

                if (year < 2000 || year > DateTime.Now.Year + 1)
                {
                    return BadRequest(new { message = "Año inválido" });
                }

                _logger.LogInformation("Obteniendo ventas del período: {Year}/{Month}", year, month);

                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var sales = await _ventaService.GetAllSalesAsync(startDate, endDate, null);

                return Ok(sales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ventas del período {Year}/{Month}", year, month);
                return StatusCode(500, new
                {
                    message = "Error al obtener ventas del período",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene las ventas de un vendedor específico
        /// </summary>
        /// <param name="sellerId">ID del vendedor</param>
        /// <param name="startDate">Fecha de inicio (opcional)</param>
        /// <param name="endDate">Fecha de fin (opcional)</param>
        /// <returns>Lista de ventas del vendedor</returns>
        [HttpGet("seller/{sellerId}")]
        [ProducesResponseType(typeof(IEnumerable<SaleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetSalesBySeller(
            int sellerId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("Obteniendo ventas del vendedor {SellerId}", sellerId);

                var sales = await _ventaService.GetAllSalesAsync(startDate, endDate, sellerId);

                return Ok(sales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ventas del vendedor {SellerId}", sellerId);
                return StatusCode(500, new
                {
                    message = "Error al obtener ventas del vendedor",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de ventas por rango de fechas
        /// </summary>
        /// <param name="startDate">Fecha de inicio</param>
        /// <param name="endDate">Fecha de fin</param>
        /// <returns>Estadísticas de ventas</returns>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetSalesStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Si no se proporcionan fechas, usar el mes actual
                if (!startDate.HasValue)
                {
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                }

                if (!endDate.HasValue)
                {
                    endDate = DateTime.Now;
                }

                if (startDate > endDate)
                {
                    return BadRequest(new { message = "La fecha de inicio debe ser menor a la fecha de fin" });
                }

                _logger.LogInformation("Obteniendo estadísticas de ventas - Desde: {Start} Hasta: {End}",
                    startDate, endDate);

                var sales = await _ventaService.GetAllSalesAsync(startDate, endDate, null);
                var salesList = sales.ToList();

                var statistics = new
                {
                    Period = new
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        Days = (endDate.Value - startDate.Value).Days + 1
                    },
                    TotalSales = salesList.Count,
                    TotalAmount = salesList.Sum(s => s.Total),
                    TotalDiscount = salesList.Sum(s => s.DiscountTotal),
                    AverageTicket = salesList.Any() ? salesList.Average(s => s.Total) : 0,
                    TopSellers = salesList
                        .GroupBy(s => s.SellerName)
                        .Select(g => new
                        {
                            SellerName = g.Key,
                            SalesCount = g.Count(),
                            TotalAmount = g.Sum(s => s.Total)
                        })
                        .OrderByDescending(x => x.TotalAmount)
                        .Take(5)
                        .ToList(),
                    SalesByDay = salesList
                        .GroupBy(s => s.SoldAt.Date)
                        .Select(g => new
                        {
                            Date = g.Key,
                            Count = g.Count(),
                            Total = g.Sum(s => s.Total)
                        })
                        .OrderBy(x => x.Date)
                        .ToList()
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de ventas");
                return StatusCode(500, new
                {
                    message = "Error al obtener estadísticas",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene el total de ventas entre dos fechas
        /// </summary>
        /// <param name="startDate">Fecha de inicio</param>
        /// <param name="endDate">Fecha de fin</param>
        /// <returns>Total de ventas</returns>
        [HttpGet("total")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetSalesTotal(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                _logger.LogInformation("Obteniendo total de ventas - Desde: {Start} Hasta: {End}",
                    startDate, endDate);

                var sales = await _ventaService.GetAllSalesAsync(startDate, endDate, null);
                var salesList = sales.ToList();

                var total = new
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalSales = salesList.Count,
                    TotalAmount = salesList.Sum(s => s.Total),
                    TotalSubtotal = salesList.Sum(s => s.Subtotal),
                    TotalDiscount = salesList.Sum(s => s.DiscountTotal)
                };

                return Ok(total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener total de ventas");
                return StatusCode(500, new
                {
                    message = "Error al obtener total",
                    error = ex.Message
                });
            }
        }
    }
}