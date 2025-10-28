// ============================================
// CreateBranchDto.cs
// ============================================
namespace ForrajeriaJovitaAPI.DTOs
{
    public class CreateBranchDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
    }
}