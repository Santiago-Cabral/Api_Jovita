namespace ForrajeriaJovitaAPI.DTOs.Sales
{
    public class UpdateSaleDto
    {
        public int Id { get; set; }
        public int? Status { get; set; }   // 1=Pendiente, 2=Pagado, 3=Cancelado
        public string? Note { get; set; }
    }
}
