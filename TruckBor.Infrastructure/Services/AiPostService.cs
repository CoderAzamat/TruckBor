using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Enums;

namespace TruckBor.Infrastructure.Services;

public class AiPostService : IAiPostService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<AiPostService> _logger;

    public AiPostService(IConfiguration config, ILogger<AiPostService> logger)
    {
        var apiKey = config["Anthropic:ApiKey"] ?? "";
        _client = new AnthropicClient(apiKey);
        _logger = logger;
    }

    public async Task<AiExtractedPost> ExtractPostFromTextAsync(string freeText, CancellationToken ct = default)
    {
        try
        {
            var systemPrompt = @"You are a logistics post parser for Uzbekistan. Extract structured data from free-text logistics ads.

Return ONLY valid JSON with these fields:
{
  ""from_city"": ""departure city/region"",
  ""to_city"": ""destination city/region"",
  ""cargo_type"": ""type of cargo (e.g. un, piyoz, mebel)"",
  ""weight"": ""weight with unit (e.g. 10 tonna)"",
  ""vehicle_type"": ""vehicle type if mentioned (e.g. tent-fura, ref, izotermal)"",
  ""price"": ""price if mentioned (e.g. 500000 so'm, kelishiladi)"",
  ""contact_phone"": ""phone number if found (format: +998XXXXXXXXX)"",
  ""post_type"": ""cargo"" or ""transport"" or ""dogruz""
}

Rules:
- If a field is not found in text, set it to null
- Detect post_type: if user offers a truck/vehicle = ""transport"", if looking for cargo space = ""dogruz"", otherwise = ""cargo""
- Normalize city names to standard Uzbek: Toshkent, Samarqand, Buxoro, Andijon, Farg'ona, Namangan, Xorazm, Surxondaryo, Qashqadaryo, Jizzax, Sirdaryo, Navoiy, Nukus, Termiz, Qarshi, Urganch, Guliston, Kokand, Marg'ilon, Chirchiq
- Phone numbers: normalize to +998XXXXXXXXX format
- Parse Uzbek (latin & cyrillic), Russian, English
- Return ONLY the JSON object, no markdown, no explanation";

            var messages = new List<Message>
            {
                new Message(RoleType.User, freeText)
            };

            var parameters = new MessageParameters
            {
                Messages = messages,
                Model = "claude-sonnet-4-20250514",
                MaxTokens = 500,
                System = new List<SystemMessage> { new SystemMessage(systemPrompt) },
                Temperature = 0m
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters, ct);
            var responseText = response.Content?.FirstOrDefault()?.ToString() ?? "";

            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd < 0)
                return new AiExtractedPost { IsSuccessful = false };

            var json = responseText[jsonStart..(jsonEnd + 1)];

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<AiRawResponse>(json, options);

            if (parsed is null || (string.IsNullOrEmpty(parsed.FromCity) && string.IsNullOrEmpty(parsed.ToCity)))
                return new AiExtractedPost { IsSuccessful = false };

            var postType = parsed.PostType?.ToLower() switch
            {
                "transport" => PostType.Transport,
                "dogruz" => PostType.Dogruz,
                _ => PostType.Cargo
            };

            return new AiExtractedPost
            {
                FromCity = parsed.FromCity,
                ToCity = parsed.ToCity,
                CargoType = parsed.CargoType,
                Weight = parsed.Weight,
                VehicleType = parsed.VehicleType,
                Price = parsed.Price,
                ContactPhone = parsed.ContactPhone,
                PostType = postType,
                IsSuccessful = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI post extraction failed for text: {Text}", freeText);
            return new AiExtractedPost { IsSuccessful = false };
        }
    }

    private class AiRawResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("from_city")]
        public string? FromCity { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("to_city")]
        public string? ToCity { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("cargo_type")]
        public string? CargoType { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("weight")]
        public string? Weight { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("vehicle_type")]
        public string? VehicleType { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("price")]
        public string? Price { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("contact_phone")]
        public string? ContactPhone { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("post_type")]
        public string? PostType { get; set; }
    }
}
