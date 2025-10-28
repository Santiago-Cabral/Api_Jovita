// Models/Configuration.cs
namespace ForrajeriaJovitaAPI.Models
{
    public class Configuration
    {
        public int Id { get; set; }
        public double PercentageIncreaseRetailPrice { get; set; }
        public double PercentageIncreaseWholesalePrice { get; set; }
        public int MinimumProductsWholesalePurchase { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;
    }
}
