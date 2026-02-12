using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForrajeriaJovitaAPI.Dtos
{
    public class ShippingZoneDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("localities")]
        public List<string> Localities { get; set; } = new List<string>();
    }

    public class SettingsDto
    {
        [JsonPropertyName("storeName")]
        public string StoreName { get; set; } = "Forrajeria Jovita";

        [JsonPropertyName("email")]
        public string Email { get; set; } = "contacto@forrajeriajovita.com";

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = "+54 9 3814669135";

        [JsonPropertyName("address")]
        public string Address { get; set; } = "Aragón 32 Yerba Buena, Argentina";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "Tu dietética de confianza con productos naturales y saludables";

        [JsonPropertyName("storeLocation")]
        public string StoreLocation { get; set; } = "Yerba Buena, Tucumán";

        [JsonPropertyName("freeShipping")]
        public bool FreeShipping { get; set; } = true;

        [JsonPropertyName("freeShippingMinimum")]
        public int FreeShippingMinimum { get; set; } = 5000;

        [JsonPropertyName("shippingCost")]
        public int ShippingCost { get; set; } = 1500;

        [JsonPropertyName("deliveryTime")]
        public string DeliveryTime { get; set; } = "24-48 horas";

        [JsonPropertyName("shippingZones")]
        public List<ShippingZoneDto> ShippingZones { get; set; } = new List<ShippingZoneDto>();

        [JsonPropertyName("defaultShippingPrice")]
        public int DefaultShippingPrice { get; set; } = 2500;

        [JsonPropertyName("cash")]
        public bool Cash { get; set; } = true;

        [JsonPropertyName("bankTransfer")]
        public bool BankTransfer { get; set; } = true;

        [JsonPropertyName("cards")]
        public bool Cards { get; set; } = true;

        [JsonPropertyName("bankName")]
        public string BankName { get; set; } = "Banco Macro";

        [JsonPropertyName("accountHolder")]
        public string AccountHolder { get; set; } = "Forrajeria Jovita S.R.L.";

        [JsonPropertyName("cbu")]
        public string Cbu { get; set; } = "0000003100010000000001";

        [JsonPropertyName("alias")]
        public string Alias { get; set; } = "JOVITA.DIETETICA";

        [JsonPropertyName("emailNewOrder")]
        public bool EmailNewOrder { get; set; } = true;

        [JsonPropertyName("emailLowStock")]
        public bool EmailLowStock { get; set; } = true;

        [JsonPropertyName("whatsappNewOrder")]
        public bool WhatsappNewOrder { get; set; } = false;

        [JsonPropertyName("whatsappLowStock")]
        public bool WhatsappLowStock { get; set; } = false;

        // Helper: devuelve DTO con defaults (si lo necesitás)
        public static SettingsDto Defaults() => new SettingsDto
        {
            ShippingZones = new List<ShippingZoneDto>
            {
                new ShippingZoneDto { Id = 1, Price = 800, Label = "Zona 1 - $800", Localities = new List<string>{ "yerba buena","san pablo","el portal" } },
                new ShippingZoneDto { Id = 2, Price = 1200, Label = "Zona 2 - $1200", Localities = new List<string>{ "san miguel de tucumán","san miguel","centro","tucumán","villa carmela","barrio norte" } },
                new ShippingZoneDto { Id = 3, Price = 1800, Label = "Zona 3 - $1800", Localities = new List<string>{ "tafí viejo","tafi viejo","banda del río salí","alderetes","las talitas" } }
            }
        };
    }

    public static class SettingsMapper
    {
        private static readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static T? TryDeserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;
            try
            {
                // Los valores en BD vienen serializados con JsonSerializer.Serialize,
                // por lo que aquí parseamos correctamente a tipos concretos.
                return JsonSerializer.Deserialize<T>(json, _jsonOpts);
            }
            catch
            {
                // Intentos fallback para tipos primitivos representados como strings
                try
                {
                    // si T es string, quitar comillas si existen
                    if (typeof(T) == typeof(string))
                    {
                        var trimmed = TrimJsonString(json);
                        return (T)(object)trimmed!;
                    }

                    var js = JsonDocument.Parse(json);
                    var root = js.RootElement;

                    if (typeof(T) == typeof(bool))
                    {
                        if (root.ValueKind == JsonValueKind.True || root.ValueKind == JsonValueKind.False)
                            return (T)(object)root.GetBoolean();

                        if (bool.TryParse(root.GetRawText().Trim('"'), out var b)) return (T)(object)b;
                    }

                    if (typeof(T) == typeof(int))
                    {
                        if (root.ValueKind == JsonValueKind.Number && root.TryGetInt32(out var i)) return (T)(object)i;
                        if (int.TryParse(root.GetRawText().Trim('"'), out var i2)) return (T)(object)i2;
                    }

                    if (typeof(T) == typeof(decimal))
                    {
                        if (root.ValueKind == JsonValueKind.Number && root.TryGetDecimal(out var d)) return (T)(object)d;
                        if (decimal.TryParse(root.GetRawText().Trim('"'), out var d2)) return (T)(object)d2;
                    }

                    if (typeof(T) == typeof(List<ShippingZoneDto>))
                    {
                        // intentar deserializar array
                        try
                        {
                            return JsonSerializer.Deserialize<T>(json, _jsonOpts);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return default;
        }

        private static string? TrimJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
                return s.Substring(1, s.Length - 2);
            return s;
        }

        /// <summary>
        /// Convierte el diccionario (Key -> JSON string) en SettingsDto.
        /// </summary>
        public static SettingsDto FromDictionary(IDictionary<string, string> dict)
        {
            var dto = SettingsDto.Defaults();

            if (dict == null || dict.Count == 0) return dto;

            // Usar TryDeserialize para cada key con fallback a defaults
            if (dict.TryGetValue("storeName", out var v)) dto.StoreName = TryDeserialize<string>(v) ?? dto.StoreName;
            if (dict.TryGetValue("email", out v)) dto.Email = TryDeserialize<string>(v) ?? dto.Email;
            if (dict.TryGetValue("phone", out v)) dto.Phone = TryDeserialize<string>(v) ?? dto.Phone;
            if (dict.TryGetValue("address", out v)) dto.Address = TryDeserialize<string>(v) ?? dto.Address;
            if (dict.TryGetValue("description", out v)) dto.Description = TryDeserialize<string>(v) ?? dto.Description;
            if (dict.TryGetValue("storeLocation", out v)) dto.StoreLocation = TryDeserialize<string>(v) ?? dto.StoreLocation;

            if (dict.TryGetValue("freeShipping", out v)) dto.FreeShipping = TryDeserialize<bool>(v) || dto.FreeShipping;
            if (dict.TryGetValue("freeShippingMinimum", out v)) dto.FreeShippingMinimum = TryDeserialize<int>(v) != 0 ? TryDeserialize<int>(v)! : dto.FreeShippingMinimum;
            if (dict.TryGetValue("shippingCost", out v)) dto.ShippingCost = TryDeserialize<int>(v) != 0 ? TryDeserialize<int>(v)! : dto.ShippingCost;
            if (dict.TryGetValue("deliveryTime", out v)) dto.DeliveryTime = TryDeserialize<string>(v) ?? dto.DeliveryTime;

            if (dict.TryGetValue("shippingZones", out v))
            {
                var zones = TryDeserialize<List<ShippingZoneDto>>(v);
                if (zones != null && zones.Count > 0) dto.ShippingZones = zones;
            }

            if (dict.TryGetValue("defaultShippingPrice", out v)) dto.DefaultShippingPrice = TryDeserialize<int>(v) != 0 ? TryDeserialize<int>(v)! : dto.DefaultShippingPrice;
            if (dict.TryGetValue("cash", out v)) dto.Cash = TryDeserialize<bool>(v) || dto.Cash;
            if (dict.TryGetValue("bankTransfer", out v)) dto.BankTransfer = TryDeserialize<bool>(v) || dto.BankTransfer;
            if (dict.TryGetValue("cards", out v)) dto.Cards = TryDeserialize<bool>(v) || dto.Cards;

            if (dict.TryGetValue("bankName", out v)) dto.BankName = TryDeserialize<string>(v) ?? dto.BankName;
            if (dict.TryGetValue("accountHolder", out v)) dto.AccountHolder = TryDeserialize<string>(v) ?? dto.AccountHolder;
            if (dict.TryGetValue("cbu", out v)) dto.Cbu = TryDeserialize<string>(v) ?? dto.Cbu;
            if (dict.TryGetValue("alias", out v)) dto.Alias = TryDeserialize<string>(v) ?? dto.Alias;

            if (dict.TryGetValue("emailNewOrder", out v)) dto.EmailNewOrder = TryDeserialize<bool>(v) || dto.EmailNewOrder;
            if (dict.TryGetValue("emailLowStock", out v)) dto.EmailLowStock = TryDeserialize<bool>(v) || dto.EmailLowStock;
            if (dict.TryGetValue("whatsappNewOrder", out v)) dto.WhatsappNewOrder = TryDeserialize<bool>(v) || dto.WhatsappNewOrder;
            if (dict.TryGetValue("whatsappLowStock", out v)) dto.WhatsappLowStock = TryDeserialize<bool>(v) || dto.WhatsappLowStock;

            return dto;
        }
    }
}
