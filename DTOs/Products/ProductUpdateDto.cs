namespace ForrajeriaJovitaAPI.DTOs.Products
{
    public class ProductUpdateDto : ProductCreateDto
    {
        public int Id { get; set; }
        public DateTime UpdateDate { get; set; } = DateTime.Now;
    }
}
