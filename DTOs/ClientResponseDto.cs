namespace ForrajeriaJovitaAPI.DTOs.Clients
{
    public class ClientResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Document { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal DebitBalance { get; set; }
    }
}
