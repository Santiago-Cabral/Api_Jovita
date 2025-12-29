using System;
using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.Models
{
    public class Client
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Document { get; set; } = string.Empty;

        // NUEVO: email para identificar al cliente
        public string? Email { get; set; }

        public decimal Amount { get; set; }
        public decimal DebitBalance { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Vincular client con user (opcional)
        public int? UserId { get; set; }
        public User? User { get; set; }

        // Navegación: ventas del cliente
        public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}
