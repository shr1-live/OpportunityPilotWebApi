using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Ai;
using OpportunityPilot.Application.Configuration;
using OpportunityPilot.Domain.Ai;

namespace OpportunityPilot.Infrastructure.Ai;

/// <summary>
/// Gemini generateContent with a per-call timeout and at most one retry, only for 429 or 5xx. The key travels in a
/// header and never appears in a reason. Reasons are short and safe to show (status codes, "timed out").
/// </summary>
public sealed class GeminiLlmClient(HttpClient http, IOptions<AiOptions> options) : ILlmClient
{
    public const int MaxAttempts = 2;
    private readonly AiOptions _options = options.Value;

    public async Task<LlmResult> GenerateJsonAsync(string operation, string system, string input, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(_options.GeminiApiKey)) return new(null, AiOutcome.ProviderError, "No key", 0, watch.Elapsed);
        string? reason = null;
        var outcome = AiOutcome.ProviderError;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
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
                if (!response.IsSuccessStatusCode)
                {
                    var code = (int)response.StatusCode;
                    reason = $"Gemini answered {code}";
                    outcome = AiOutcome.ProviderError;
                    if ((code == 429 || code >= 500) && attempt < MaxAttempts) { await Task.Delay(TimeSpan.FromSeconds(1), ct); continue; }
                    return new(null, outcome, reason, attempt, watch.Elapsed);
                }
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
                var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                return string.IsNullOrWhiteSpace(text)
                    ? new(null, AiOutcome.InvalidResponse, "Gemini returned no text", attempt, watch.Elapsed)
                    : new(text, AiOutcome.Succeeded, null, attempt, watch.Elapsed);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new(null, AiOutcome.Timeout, $"Timed out after {Math.Clamp(_options.TimeoutSeconds, 5, 60)} s", attempt, watch.Elapsed);
            }
            catch (HttpRequestException ex)
            {
                reason = ex.StatusCode is { } s ? $"Network error ({(int)s})" : "Network error";
                if (attempt < MaxAttempts) { await Task.Delay(TimeSpan.FromSeconds(1), ct); continue; }
                return new(null, AiOutcome.ProviderError, reason, attempt, watch.Elapsed);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
            {
                return new(null, AiOutcome.InvalidResponse, "Gemini's answer was not in the expected shape", attempt, watch.Elapsed);
            }
        }
        return new(null, outcome, reason, MaxAttempts, watch.Elapsed);
    }
}
