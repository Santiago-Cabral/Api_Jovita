using ForrajeriaJovitaAPI.DTOs;

namespace ForrajeriaJovitaAPI.Services
{
    public interface IBranchService
    {
        Task<List<BranchDto>> GetAllBranchesAsync(bool? isActived = null);
        Task<BranchDto?> GetBranchByIdAsync(int id);
        Task<BranchDto> CreateBranchAsync(CreateBranchDto dto);
        Task<BranchDto?> UpdateBranchAsync(int id, CreateBranchDto dto);
        Task<bool> DeleteBranchAsync(int id);
    }
}
