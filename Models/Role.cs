// Models/Role.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public ICollection<RoleAccess> RolesAccesses { get; set; } = new List<RoleAccess>();
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}