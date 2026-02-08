using System.Text.Json;

namespace MathLearning.TranslationJob.Services;

public interface ITranslationClient
{
    Task<string> TranslateTextAsync(string text, string fromLang, string toLang);
}

public class GoogleTranslateClient : ITranslationClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public GoogleTranslateClient(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<string> TranslateTextAsync(string text, string fromLang, string toLang)
    {
        // Google Translate API v2
        var url = $"https://translation.googleapis.com/language/translate/v2?key={_apiKey}";

        var payload = new
        {
            q = text,
            source = fromLang,
            target = toLang,
            format = "text"
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GoogleTranslateResponse>(json);

        return result?.Data?.Translations?.FirstOrDefault()?.TranslatedText ?? text;
    }

    private class GoogleTranslateResponse
    {
        public GoogleTranslateData? Data { get; set; }
    }

    private class GoogleTranslateData
    {
        public List<GoogleTranslation>? Translations { get; set; }
    }

    private class GoogleTranslation
    {
        public string? TranslatedText { get; set; }
    }
}

public class DeepLTranslateClient : ITranslationClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public DeepLTranslateClient(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<string> TranslateTextAsync(string text, string fromLang, string toLang)
    {
        // DeepL API
        var url = "https://api.deepl.com/v2/translate";

        var payload = new Dictionary<string, string>
        {
            ["text"] = text,
            ["source_lang"] = fromLang.ToUpper(),
            ["target_lang"] = toLang.ToUpper()
        };

        var content = new FormUrlEncodedContent(payload);
        content.Headers.Add("Authorization", $"DeepL-Auth-Key {_apiKey}");

        var response = await _http.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<DeepLResponse>(json);

        return result?.Translations?.FirstOrDefault()?.Text ?? text;
    }

    private class DeepLResponse
    {
        public List<DeepLTranslation>? Translations { get; set; }
    }

    private class DeepLTranslation
    {
        public string? Text { get; set; }
    }
}
