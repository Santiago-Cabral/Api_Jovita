// ============================================
// CreateClientDto.cs
// ============================================
namespace ForrajeriaJovitaAPI.DTOs
{
    public class CreateClientDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? DocumentNumber { get; set; }
    }
}