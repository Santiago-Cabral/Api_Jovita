// Models/User.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // Nuevo: Email real del usuario
        public string Email { get; set; } = string.Empty;

        // Para compatibilidad con sistemas previos
        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }
        public bool IsActived { get; set; }
        public int? RoleId { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public Role? Role { get; set; }
        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
        public ICollection<CashSession> OpenedCashSessions { get; set; } = new List<CashSession>();
        public ICollection<CashSession> ClosedCashSessions { get; set; } = new List<CashSession>();
    }
}
