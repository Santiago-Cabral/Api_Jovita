// CreateUserDto.cs - CORREGIDO
namespace ForrajeriaJovitaAPI.DTOs
{
    public class CreateUserDto
    {
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int? RoleId { get; set; }
    }
}