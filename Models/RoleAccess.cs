// Models/RoleAccess.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class RoleAccess
    {
        public int Id { get; set; }
        public int AccessId { get; set; }
        public int RoleId { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public Access Access { get; set; } = null!;
        public Role Role { get; set; } = null!;
    }
}