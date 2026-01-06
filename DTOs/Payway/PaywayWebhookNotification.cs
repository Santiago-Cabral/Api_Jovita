namespace ForrajeriaJovitaAPI.DTOs.Payway
{
	public class PaywayWebhookNotification
	{
		public string? SiteTransactionId { get; set; }
		public string? Status { get; set; }
		public string? StatusDetail { get; set; }
		public decimal Amount { get; set; }
	}
}
