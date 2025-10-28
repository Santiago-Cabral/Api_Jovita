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
    public class StockController : ControllerBase
    {
        private readonly IStockService _stockService;

        public StockController(IStockService stockService)
        {
            _stockService = stockService;
        }

        // GET: api/stock
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductStockDto>>> GetAllStock(
            [FromQuery] int? branchId = null,
            [FromQuery] int? productId = null)
        {
            try
            {
                var stocks = await _stockService.GetAllStockAsync(branchId, productId);
                return Ok(stocks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener stock", error = ex.Message });
            }
        }

        // GET: api/stock/branch/1
        [HttpGet("branch/{branchId}")]
        public async Task<ActionResult<IEnumerable<ProductStockDto>>> GetStockByBranch(int branchId)
        {
            try
            {
                var stocks = await _stockService.GetStockByBranchAsync(branchId);
                return Ok(stocks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener stock", error = ex.Message });
            }
        }

        // GET: api/stock/product/1
        [HttpGet("product/{productId}")]
        public async Task<ActionResult<IEnumerable<ProductStockDto>>> GetStockByProduct(int productId)
        {
            try
            {
                var stocks = await _stockService.GetStockByProductAsync(productId);
                return Ok(stocks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener stock", error = ex.Message });
            }
        }

        // POST: api/stock/update
        [HttpPost("update")]
        public async Task<IActionResult> UpdateStock(UpdateStockDto dto)
        {
            try
            {
                var result = await _stockService.UpdateStockAsync(dto);

                if (!result)
                {
                    return NotFound(new { message = "Producto o sucursal no encontrados" });
                }

                return Ok(new { message = "Stock actualizado correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al actualizar stock", error = ex.Message });
            }
        }

        // POST: api/stock/add
        [HttpPost("add")]
        public async Task<IActionResult> AddStock(UpdateStockDto dto)
        {
            try
            {
                var result = await _stockService.AddStockAsync(dto);

                if (!result)
                {
                    return NotFound(new { message = "Producto o sucursal no encontrados" });
                }

                return Ok(new { message = "Stock agregado correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al agregar stock", error = ex.Message });
            }
        }

        // GET: api/stock/low
        [HttpGet("low")]
        public async Task<ActionResult<IEnumerable<object>>> GetLowStock([FromQuery] decimal threshold = 10)
        {
            try
            {
                var lowStock = await _stockService.GetLowStockAsync(threshold);
                return Ok(lowStock);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener stock bajo", error = ex.Message });
            }
        }
    }
}
