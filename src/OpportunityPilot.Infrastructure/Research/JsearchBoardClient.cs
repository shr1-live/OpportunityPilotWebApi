using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Configuration;
using OpportunityPilot.Application.JobBoards;

namespace OpportunityPilot.Infrastructure.Research;

/// <summary>
/// Live job-board postings (Indeed, LinkedIn, SEEK) through JSearch. The key travels only in a request header and is never
/// logged. Identical searches are served from memory for <see cref="JsearchOptions.CacheMinutes"/> so the free quota lasts.
/// </summary>
public sealed class JsearchBoardClient(HttpClient http, IOptions<JsearchOptions> options, TimeProvider clock,
    ILogger<JsearchBoardClient> logger, Application.Common.OperationalMetrics metrics) : IJobBoardSearch
{
    private static readonly ConcurrentDictionary<string, JobBoardSearchResult> Cache = new();

    public bool Configured => options.Value.Configured;

    public async Task<JobBoardSearchResult> SearchAsync(JobBoardSearchRequest request, CancellationToken ct)
    {
        var o = options.Value;
        var name = JsearchBoardParser.Name(request.Board);
        var source = $"JSearch (Google for Jobs), {name} postings only";
        if (!o.Configured)
            return new(request.Board, "NotConfigured", [], 0, $"Live {name} listings need a JSearch key on the server (Jsearch__Key).", null, false, source);

        var query = string.IsNullOrWhiteSpace(request.Location) ? request.Query : $"{request.Query} in {request.Location}";
        // Adding the board name steers Google for Jobs towards that board's copy of each posting.
        var parameters = new Dictionary<string, string>
        {
            ["query"] = $"{query} via {name}", ["page"] = request.Page.ToString(), ["num_pages"] = "1", ["date_posted"] = request.DatePosted,
        };
        // v5 names the remote filter work_from_home; remote_jobs_only is the earlier name. Unknown parameters are ignored.
        if (request.RemoteOnly) { parameters["work_from_home"] = "true"; parameters["remote_jobs_only"] = "true"; }
        var country = request.Board == JobBoard.Seek && string.IsNullOrWhiteSpace(request.Country) ? "au" : request.Country;
        if (!string.IsNullOrWhiteSpace(country)) parameters["country"] = country.ToLowerInvariant();
        var qs = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

        var now = clock.GetUtcNow().UtcDateTime;
        var cacheKey = $"{request.Board}|{qs}";
        if (Cache.TryGetValue(cacheKey, out var cached) && cached.ObservedAt > now.AddMinutes(-o.CacheMinutes))
            return cached with { FromCache = true };

        using var message = new HttpRequestMessage(HttpMethod.Get, $"https://{o.Host}/{o.SearchPath.Trim('/')}?{qs}");
        message.Headers.Add("X-RapidAPI-Key", o.Key);
        message.Headers.Add("X-RapidAPI-Host", o.Host);
        var started = clock.GetTimestamp();
        try
        {
            using var response = await http.SendAsync(message, ct);
            metrics.Record("jsearch.search", clock.GetElapsedTime(started), response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                var code = (int)response.StatusCode;
                logger.LogWarning("JSearch returned {Status}", code);
                var why = code switch
                {
                    401 or 403 => "The JSearch key was refused. Check Jsearch__Key and that the RapidAPI plan is subscribed.",
                    429 => "The JSearch monthly or hourly quota is used up. Try again later.",
                    _ => $"JSearch answered {code}. Try again later.",
                };
                return new(request.Board, "Failed", [], 0, why, now, false, source);
            }
            var (jobs, total) = JsearchBoardParser.Parse(await response.Content.ReadAsStringAsync(ct), request.Board);
            var result = new JobBoardSearchResult(request.Board, "Ready", jobs, total,
                jobs.Count == 0 ? $"JSearch returned {total} postings; none were published on {name}. Try a broader search." : null,
                now, false, source);
            Cache[cacheKey] = result;
            return result;
        }
        catch (Exception e) when ((e is HttpRequestException or TaskCanceledException or JsonException) && !ct.IsCancellationRequested)
        {
            logger.LogWarning("JSearch request failed: {Type}", e.GetType().Name);
            metrics.Record("jsearch.search", clock.GetElapsedTime(started), e.GetType().Name);
            return new(request.Board, "Failed", [], 0, "JSearch could not be reached. Try again later.", now, false, source);
        }
    }
}
