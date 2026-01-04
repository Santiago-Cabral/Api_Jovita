using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ForrajeriaJovitaAPI.Services
{
	public class PaywayService : IPaywayService
	{
		private readonly IConfiguration _config;
		private readonly HttpClient _http;

		public PaywayService(IConfiguration config, HttpClient http)
		{
			_config = config;
			_http = http;
		}

		public async Task<string> CreatePayment(decimal amount, string description)
		{
			var merchant = _config["Payway:MerchantId"];
			var publicKey = _config["Payway:PublicKey"];
			var privateKey = _config["Payway:PrivateKey"];
			var url = _config["Payway:ApiUrl"];

			var credentials = Convert.ToBase64String(
				Encoding.UTF8.GetBytes($"{publicKey}:{privateKey}")
			);

			_http.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Basic", credentials);

			var body = new
			{
				site_transaction_id = Guid.NewGuid().ToString(),
				token = "",
				payment_method_id = 1,
				bin = "450799",
				amount = amount,
				currency = "ARS",
				installments = 1,
				description = description
			};

			var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

			var response = await _http.PostAsync($"{url}/payments", content);

			var result = await response.Content.ReadAsStringAsync();

			return result;
		}
	}

	public interface IPaywayService
	{
		Task<string> CreatePayment(decimal amount, string description);
	}
}
