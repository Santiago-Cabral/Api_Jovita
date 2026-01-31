using Microsoft.AspNetCore.Mvc;
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

        // =============================
        // GET ALL
        // =============================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BranchDto>>> GetBranches([FromQuery] bool? isActived = null)
        {
            var branches = await _branchService.GetAllBranchesAsync(isActived);
            return Ok(branches);
        }

        // =============================
        // GET BY ID
        // =============================
        [HttpGet("{id}")]
        public async Task<ActionResult<BranchDto>> GetBranch(int id)
        {
            var branch = await _branchService.GetBranchByIdAsync(id);

            if (branch == null)
                return NotFound(new { message = "Sucursal no encontrada" });

            return Ok(branch);
        }

        // =============================
        // CREATE
        // =============================
        [HttpPost]
        public async Task<ActionResult<BranchDto>> CreateBranch([FromBody] CreateBranchDto dto)
        {
            var branch = await _branchService.CreateBranchAsync(dto);
            return CreatedAtAction(nameof(GetBranch), new { id = branch.Id }, branch);
        }

        // =============================
        // UPDATE (EL QUE FALTABA)
        // =============================
        [HttpPut("{id}")]
        public async Task<ActionResult<BranchDto>> UpdateBranch(int id, [FromBody] CreateBranchDto dto)
        {
            var branch = await _branchService.UpdateBranchAsync(id, dto);

            if (branch == null)
                return NotFound(new { message = "Sucursal no encontrada" });

            return Ok(branch);
        }

        [HttpPatch("{id}/active")]
        public async Task<IActionResult> SetActive(int id, [FromQuery] bool value)
        {
            var ok = await _branchService.SetBranchActiveAsync(id, value);

            if (!ok)
                return NotFound(new { message = "Sucursal no encontrada" });

            return Ok(new { message = "Estado actualizado", isActive = value });
        }


        // =============================
        // DELETE (SOFT)
        // =============================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBranch(int id)
        {
            var result = await _branchService.DeleteBranchAsync(id);

            if (!result)
                return NotFound(new { message = "Sucursal no encontrada" });

            return Ok(new { message = "Sucursal eliminada correctamente" });
        }
    }
}
