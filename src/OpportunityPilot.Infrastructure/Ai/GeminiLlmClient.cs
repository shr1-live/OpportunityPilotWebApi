using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Ai;
using OpportunityPilot.Application.Configuration;

namespace OpportunityPilot.Infrastructure.Ai;

public sealed class GeminiLlmClient(HttpClient http, IOptions<AiOptions> options) : ILlmClient
{
    private readonly AiOptions _options = options.Value;

    public async Task<string?> GenerateJsonAsync(string operation, string system, string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.GeminiApiKey)) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60)));
        var model = Uri.EscapeDataString(_options.GeminiModel);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{model}:generateContent");
        request.Headers.Add("x-goog-api-key", _options.GeminiApiKey);
        request.Content = JsonContent.Create(new
        {
            systemInstruction = new { parts = new[] { new { text = system } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = $"Operation: {operation}\n{input}" } } } },
            generationConfig = new { responseMimeType = "application/json", maxOutputTokens = Math.Clamp(_options.MaxOutputTokens, 128, 4096), temperature = 0.2 }
        });
        try
        {
            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) return null;
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException) { return null; }
    }
}
