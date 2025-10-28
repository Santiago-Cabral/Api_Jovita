// UserDto.cs - CORREGIDO
namespace ForrajeriaJovitaAPI.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public bool IsActived { get; set; }
        public string? RoleName { get; set; }
    }
}