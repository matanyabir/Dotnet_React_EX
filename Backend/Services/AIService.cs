using System.Text;

namespace Backend.Services;

public class AIService : IAIService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public AIService(IConfiguration configuration, ILogger<AIService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string?> GenerateSummaryAsync(string description)
    {
        try
        {
            var apiKey = _configuration["AI:ApiKey"];
            var apiUrl = _configuration["AI:ApiUrl"] ?? "https://api.openai.com/v1/chat/completions";
            var model = _configuration["AI:Model"] ?? "gpt-3.5-turbo";
            
            if (string.IsNullOrEmpty(apiKey))
            {
                return GenerateBasicSummary(description);
            }
            
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = "אתה עוזר שמסכם בעיות תמיכה טכניות בעברית. תן סיכום קצר ומדויק." },
                    new { role = "user", content = $"סכם את הבעיה הבאה: {description}" }
                },
                max_tokens = 100,
                temperature = 0.7
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            
            var response = await httpClient.PostAsync(apiUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<OpenAIResponse>(responseJson);
                
                return result?.choices?[0]?.message?.content?.Trim();
            }
            else
            {
                _logger.LogWarning($"שגיאה ב-API של AI: {response.StatusCode}");
                return GenerateBasicSummary(description);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "שגיאה ביצירת AI Summary");
            return GenerateBasicSummary(description);
        }
    }

    private string GenerateBasicSummary(string description)
    {
        if (string.IsNullOrEmpty(description))
            return null;
        
        if (description.Length <= 50)
            return description;
        
        return description.Substring(0, 50) + "...";
    }
}

public class OpenAIResponse
{
    public Choice[]? choices { get; set; }
}

public class Choice
{
    public Message? message { get; set; }
}

public class Message
{
    public string? content { get; set; }
}

