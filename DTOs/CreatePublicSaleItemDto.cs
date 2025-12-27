// DTOs/CreatePublicSaleItemDto.cs
using System.ComponentModel.DataAnnotations;

namespace ForrajeriaJovitaAPI.DTOs
{
    /// <summary>
    /// DTO para un item individual en una venta pública (carrito web)
    /// </summary>
    public class CreatePublicSaleItemDto
    {
        /// <summary>
        /// ID del producto a comprar
        /// </summary>
        [Required(ErrorMessage = "ProductId es requerido")]
        [Range(1, int.MaxValue, ErrorMessage = "ProductId debe ser mayor a 0")]
        public int ProductId { get; set; }

        /// <summary>
        /// Cantidad de unidades del producto
        /// </summary>
        [Required(ErrorMessage = "Quantity es requerido")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity debe ser al menos 1")]
        public int Quantity { get; set; }

        /// <summary>
        /// Precio unitario del producto
        /// </summary>
        [Required(ErrorMessage = "UnitPrice es requerido")]
        [Range(0, double.MaxValue, ErrorMessage = "UnitPrice debe ser mayor o igual a 0")]
        public decimal UnitPrice { get; set; }
    }
}