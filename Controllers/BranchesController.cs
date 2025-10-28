using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Data;
using ForrajeriaJovitaAPI.Models;
using ForrajeriaJovitaAPI.DTOs;
using ForrajeriaJovitaAPI.Services;

namespace ForrajeriaJovitaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BranchesController : ControllerBase
    {
        private readonly IBranchService _branchService;

        public BranchesController(IBranchService branchService)
        {
            _branchService = branchService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BranchDto>>> GetBranches([FromQuery] bool? isActived = null)
        {
            try
            {
                var branches = await _branchService.GetAllBranchesAsync(isActived);
                return Ok(branches);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener sucursales", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BranchDto>> GetBranch(int id)
        {
            try
            {
                var branch = await _branchService.GetBranchByIdAsync(id);

                if (branch == null)
                {
                    return NotFound(new { message = "Sucursal no encontrada" });
                }

                return Ok(branch);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener sucursal", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult<BranchDto>> CreateBranch(CreateBranchDto dto)
        {
            try
            {
                var branch = await _branchService.CreateBranchAsync(dto);
                return CreatedAtAction(nameof(GetBranch), new { id = branch.Id }, branch);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al crear sucursal", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBranch(int id)
        {
            try
            {
                var result = await _branchService.DeleteBranchAsync(id);

                if (!result)
                {
                    return NotFound(new { message = "Sucursal no encontrada" });
                }

                return Ok(new { message = "Sucursal eliminada correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al eliminar sucursal", error = ex.Message });
            }
        }
    }
}
