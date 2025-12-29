using System;
using System.Collections.Generic;

namespace ForrajeriaJovitaAPI.Models
{
    public enum CashMovementType
    {
        Sale = 1,
        Other = 7
    }

    public class CashMovement
    {
        public int Id { get; set; }
        public int CashSessionId { get; set; }
        public virtual CashSession? CashSession { get; set; }

        public CashMovementType Type { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        public string? TypeOfSale { get; set; }
        public bool MovementCancelled { get; set; } = false;

        // Opcional: asignar cliente al movimiento para trazabilidad
        public int? ClientId { get; set; }
        public Client? Client { get; set; }

        public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}
