using System;
using System.Collections.Generic; // Necesario para ICollection

namespace ForrajeriaJovitaAPI.Models
{
    public class CashMovement
    {
        public int Id { get; set; }
        public int CashSessionId { get; set; }

        public CashMovementType Type { get; set; }

        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Propiedades que agregaste anteriormente
        public string? TypeOfSale { get; set; }
        public bool MovementCancelled { get; set; } = false;
        public int? ClientId { get; set; }

        // --- PROPIEDADES DE NAVEGACIÓN (Faltaban estas) ---

        // 1. Relación con el Cliente (ya la tenías, la dejo aquí)
        public Client? Client { get; set; }

        // 2. Relación con la Sesión de Caja (CORRECCIÓN DEL ERROR LÍNEA 71)
        // El contexto espera un objeto CashSession, no solo el ID.
        public virtual CashSession? CashSession { get; set; }

        // 3. Relación con Ventas (CORRECCIÓN DEL ERROR LÍNEA 227)
        // El contexto espera una lista o colección de ventas asociadas a este movimiento.
        public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}