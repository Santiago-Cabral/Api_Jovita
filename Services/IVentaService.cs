// Services/Interfaces/IVentaService.cs
using ForrajeriaJovitaAPI.DTOs;

namespace ForrajeriaJovitaAPI.Services
{
    /// <summary>
    /// Interfaz para el servicio de ventas
    /// Maneja tanto ventas internas (caja) como ventas públicas (web/carrito)
    /// </summary>
    public interface IVentaService
    {
        /// <summary>
        /// Obtiene todas las ventas con filtros opcionales
        /// </summary>
        /// <param name="start">Fecha de inicio (opcional)</param>
        /// <param name="end">Fecha de fin (opcional)</param>
        /// <param name="sellerId">ID del vendedor (opcional)</param>
        /// <returns>Lista de ventas</returns>
        Task<IEnumerable<SaleDto>> GetAllSalesAsync(
            DateTime? start,
            DateTime? end,
            int? sellerId);

        /// <summary>
        /// Obtiene una venta específica por ID
        /// </summary>
        /// <param name="id">ID de la venta</param>
        /// <returns>Venta o null si no existe</returns>
        Task<SaleDto?> GetSaleByIdAsync(int id);

        /// <summary>
        /// Crea una venta interna (desde caja, requiere autenticación)
        /// </summary>
        /// <param name="dto">Datos de la venta</param>
        /// <returns>Venta creada</returns>
        Task<SaleDto> CreateSaleAsync(CreateSaleDto dto);

        /// <summary>
        /// Crea una venta pública (desde web/carrito, sin autenticación)
        /// </summary>
        /// <param name="dto">Datos de la venta pública</param>
        /// <returns>Venta creada</returns>
        Task<SaleDto> CreatePublicSaleAsync(CreatePublicSaleDto dto);

        /// <summary>
        /// Actualiza una venta existente
        /// </summary>
        /// <param name="id">ID de la venta</param>
        /// <param name="dto">Datos a actualizar</param>
        /// <returns>Venta actualizada o null si no existe</returns>
        Task<SaleDto?> UpdateSaleAsync(int id, UpdateSaleDto dto);

        /// <summary>
        /// Obtiene resumen de ventas del día actual
        /// </summary>
        /// <returns>Objeto con estadísticas del día</returns>
        Task<object> GetTodaySalesSummaryAsync();
    }
}