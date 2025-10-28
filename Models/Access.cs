// Models/Access.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class Access
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public ICollection<RoleAccess> RolesAccesses { get; set; } = new List<RoleAccess>();
    }
}


























