// Models/CashMovement.cs
using System;

namespace ForrajeriaJovitaAPI.Models
{
    public class CashMovement
    {
        public int Id { get; set; }
        public int CashSessionId { get; set; }

        // Si en tu proyecto ya usás un enum CashMovementType, mantenelo:
        public CashMovementType Type { get; set; }

        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;

        // --- PROPIEDADES NUEVAS que el service necesita ---
        // Coinciden con las columnas que mostrás en la BD (TypeOfSale, MovementCancelled, ClientId)
        public string? TypeOfSale { get; set; }          // p. ej. "Web", "Sucursal", etc.
        public bool MovementCancelled { get; set; } = false;
        public int? ClientId { get; set; }               // nullable si algunas entradas no tienen cliente

        // Opcional: navegación si tenés entidad Client/User
         public Client? Client { get; set; }

        // (mantén otras propiedades/navegaciones que ya tengas)
    }
}

