namespace ForrajeriaJovitaAPI.Models
{
    public class ProductStock
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public int BranchId { get; set; }

        // AHORA DECIMAL ✔
        public decimal Quantity { get; set; }


        public DateTime CreationDate { get; set; } = DateTime.Now;

        // Navegación
        public Product Product { get; set; } = null!;
        public Branch Branch { get; set; } = null!;
    }
}
