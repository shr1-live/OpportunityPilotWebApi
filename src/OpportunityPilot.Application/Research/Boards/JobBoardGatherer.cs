using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Application.Configuration;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.Application.Research.Boards;

/// <param name="Items">Candidates read, already bounded by the run's room.</param>
/// <param name="Requests">HTTP requests sent (retries and redirects included), charged to the run's fetch budget.</param>
public sealed record BoardGathered(SourceStatus Status, string? SafeError, IReadOnlyList<Candidate> Items, int Requests, string Message, EventLevel Level);

/// <summary>
/// Reads the documented public job-board APIs (Greenhouse, Lever, Adzuna) through <see cref="IWebFetcher.FetchJsonAsync"/>,
/// so every request gets the same address policy, size cap, timeout and redirect checks as any other research fetch.
/// No login, no scraping: these endpoints exist to publish job listings. Messages never contain a request URL (the
/// Adzuna one carries the server's key).
/// </summary>
public sealed class JobBoardGatherer(IWebFetcher fetcher, IContentParser parser, IOptions<ResearchOptions> research, IOptions<AdzunaOptions> adzuna)
{
    public const int AdzunaMaxKeywords = 3;
    public const int AdzunaResultsPerPage = 50;
    public const int AdzunaMaxDaysOld = 14;
    public const int LeverLimit = 100;

    /// <summary>Greenhouse reads one description per request; the run is kept alive (lease, cancel check) every this many.</summary>
    public const int KeepAliveEvery = 10;

    private readonly ResearchOptions _limits = research.Value;

    /// <param name="keepGoing">Renews the job's lease and reports whether to continue (false once a cancel was requested).</param>
    public Task<BoardGathered> GatherAsync(Source source, CampaignCriteria criteria, int room, int fetchesLeft,
        Func<CancellationToken, Task<bool>> keepGoing, CancellationToken ct) => source.Kind switch
    {
        SourceKind.Greenhouse => GreenhouseAsync(source, criteria, room, fetchesLeft, keepGoing, ct),
        SourceKind.Lever => LeverAsync(source, room, fetchesLeft, ct),
        SourceKind.Adzuna => AdzunaAsync(source, criteria, room, fetchesLeft, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(source), "Not a job-board source.")
    };

    private async Task<BoardGathered> GreenhouseAsync(Source source, CampaignCriteria criteria, int room, int fetchesLeft,
        Func<CancellationToken, Task<bool>> keepGoing, CancellationToken ct)
    {
        if (BoardIdentifiers.Greenhouse(source.Url) is not { } token)
            return Failed(source, "The Greenhouse board token is not valid. Delete the source and add it again.", 0);
        if (fetchesLeft <= 0) return FetchLimitReached(source);

        var root = Base(_limits.GreenhouseApiBase, ResearchOptions.DefaultGreenhouseApiBase);
        var list = await fetcher.FetchJsonAsync($"{root}/v1/boards/{token}/jobs", ct);
        var requests = list.Requests;
        if (!list.Ok)
            return Failed(source, list.StatusCode == 404
                ? $"No public Greenhouse board was found for \"{token}\". Check the board token."
                : list.FailureReason ?? "The board could not be read.", requests);
        if (BoardMapping.GreenhouseJobs(list.Content) is not { } jobs)
            return Failed(source, "Greenhouse sent a response that could not be read.", requests);
        if (jobs.Count == 0)
            return new(SourceStatus.Skipped, "The Greenhouse board has no open jobs.", [], requests, $"{source.Label}: the board has no open jobs.",
                EventLevel.Warning);

        var terms = BoardMapping.TitleTerms(criteria);
        var matched = BoardMapping.Prefilter(jobs, terms);
        var items = new List<Candidate>();
        int unreadable = 0;
        string? stoppedBy = null;
        foreach (var job in matched)
        {
            if (items.Count >= room) { stoppedBy = $"the run's limit of {_limits.EffectiveCandidates} candidates"; break; }
            if (fetchesLeft - requests <= 0) { stoppedBy = $"the run's limit of {_limits.EffectiveFetches} fetches"; break; }
            if (items.Count > 0 && items.Count % KeepAliveEvery == 0 && !await keepGoing(ct)) { stoppedBy = "a cancel request"; break; }

            var detail = await fetcher.FetchJsonAsync($"{root}/v1/boards/{token}/jobs/{Uri.EscapeDataString(job.Id)}", ct);
            requests += detail.Requests;
            var description = detail.Ok ? BoardMapping.GreenhouseDescription(detail.Content, parser) : null;
            if (description is null) unreadable++;
            // A job whose description failed is still listed: the rules mark what they cannot see as unknown.
            items.Add(BoardMapping.GreenhouseCandidate(source, token, job, description));
        }

        var matchedText = terms.Count == 0
            ? $"{Count(matched.Count, "job")} kept (no keywords or required skills to match titles against)"
            : $"{matched.Count} matched your keywords";
        var message = $"{source.Label}: {Count(jobs.Count, "job")} listed, {matchedText}, {items.Count} read.";
        if (stoppedBy is not null) message += $" Stopped at {stoppedBy}.";
        if (unreadable > 0) message += $" {Count(unreadable, "description")} could not be loaded, so those jobs are scored on the title only.";
        return new(SourceStatus.Ok, null, items, requests, message, stoppedBy is null && unreadable == 0 ? EventLevel.Info : EventLevel.Warning);
    }

    private async Task<BoardGathered> LeverAsync(Source source, int room, int fetchesLeft, CancellationToken ct)
    {
        if (BoardIdentifiers.Lever(source.Url) is not { } slug)
            return Failed(source, "The Lever company slug is not valid. Delete the source and add it again.", 0);
        if (fetchesLeft <= 0) return FetchLimitReached(source);

        var root = Base(_limits.LeverApiBase, ResearchOptions.DefaultLeverApiBase);
        var result = await fetcher.FetchJsonAsync($"{root}/v0/postings/{slug}?mode=json&limit={LeverLimit}", ct);
        if (!result.Ok)
            return Failed(source, result.StatusCode == 404
                ? $"No public Lever postings were found for \"{slug}\". Check the company slug."
                : result.FailureReason ?? "The postings could not be read.", result.Requests);
        if (BoardMapping.LeverCandidates(result.Content, source, slug, parser) is not { } postings)
            return Failed(source, "Lever sent a response that could not be read.", result.Requests);
        if (postings.Count == 0)
            return new(SourceStatus.Skipped, "The company has no open postings on Lever.", [], result.Requests,
                $"{source.Label}: no open postings.", EventLevel.Warning);

        var items = postings.Take(room).ToList();
        var message = $"{source.Label}: {Count(postings.Count, "posting")} listed, {items.Count} read.";
        if (items.Count < postings.Count) message += $" Stopped at the run's limit of {_limits.EffectiveCandidates} candidates.";
        return new(SourceStatus.Ok, null, items, result.Requests, message, items.Count < postings.Count ? EventLevel.Warning : EventLevel.Info);
    }

    private async Task<BoardGathered> AdzunaAsync(Source source, CampaignCriteria criteria, int room, int fetchesLeft, CancellationToken ct)
    {
        var keys = adzuna.Value;
        if (!keys.Configured) return Failed(source, "Adzuna is not configured on the server.", 0);
        var keywords = criteria.Keywords.Take(AdzunaMaxKeywords).ToList();
        if (keywords.Count == 0)
            return new(SourceStatus.Skipped, "Adzuna searches by keyword: add keywords to the campaign.", [], 0,
                $"{source.Label}: skipped, the campaign has no keywords to search for.", EventLevel.Warning);
        if (fetchesLeft <= 0) return FetchLimitReached(source);

        var where = criteria.Locations.FirstOrDefault(l => !l.Trim().Equals("Remote", StringComparison.OrdinalIgnoreCase));
        var root = Base(_limits.AdzunaApiBase, ResearchOptions.DefaultAdzunaApiBase);
        var found = new Dictionary<string, Candidate>(StringComparer.Ordinal);
        var requests = 0;
        var searched = 0;
        string? failure = null;
        string? stoppedBy = null;
        foreach (var keyword in keywords)
        {
            if (fetchesLeft - requests <= 0) { stoppedBy = $"the run's limit of {_limits.EffectiveFetches} fetches"; break; }
            // Built here and passed straight to the fetcher: this URL holds the key, so it is never logged or recorded.
            var url = $"{root}/v1/api/jobs/in/search/1?app_id={Uri.EscapeDataString(keys.AppId!)}&app_key={Uri.EscapeDataString(keys.AppKey!)}" +
                      $"&what={Uri.EscapeDataString(keyword)}" + (where is null ? "" : $"&where={Uri.EscapeDataString(where)}") +
                      $"&results_per_page={AdzunaResultsPerPage}&max_days_old={AdzunaMaxDaysOld}&content-type=application/json";
            var result = await fetcher.FetchJsonAsync(url, ct);
            requests += result.Requests;
            if (!result.Ok)
            {
                failure ??= result.StatusCode is 401 or 403
                    ? "Adzuna rejected the server's keys. Check Adzuna:AppId and Adzuna:AppKey."
                    : result.FailureReason ?? "Adzuna could not be read.";
                continue;
            }
            if (BoardMapping.AdzunaCandidates(result.Content, source, parser) is not { } jobs)
            {
                failure ??= "Adzuna sent a response that could not be read.";
                continue;
            }
            searched++;
            foreach (var job in jobs) found.TryAdd(job.ExternalId!, job);
        }

        if (searched == 0) return Failed(source, failure ?? "Adzuna could not be read.", requests);
        var items = found.Values.Take(room).ToList();
        var place = where is null ? "" : $" in {where}";
        var message = $"{source.Label}: searched {Count(searched, "keyword")}{place}, {Count(found.Count, "job")} found, {items.Count} read.";
        if (items.Count < found.Count) message += $" Stopped at the run's limit of {_limits.EffectiveCandidates} candidates.";
        if (stoppedBy is not null) message += $" Stopped at {stoppedBy}.";
        if (failure is not null) message += $" Some searches failed: {failure}";
        var level = items.Count < found.Count || stoppedBy is not null || failure is not null ? EventLevel.Warning : EventLevel.Info;
        return new(SourceStatus.Ok, null, items, requests, message, level);
    }

    private static BoardGathered Failed(Source source, string reason, int requests) =>
        new(SourceStatus.Failed, reason, [], requests, $"{source.Label}: could not be read safely — {reason}", EventLevel.Warning);

    private BoardGathered FetchLimitReached(Source source) =>
        new(SourceStatus.Skipped, $"Skipped: this run already used its {_limits.EffectiveFetches} page fetches.", [], 0,
            $"{source.Label}: skipped, fetch limit reached.", EventLevel.Warning);

    private static string Base(string? configured, string fallback) =>
        (string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim()).TrimEnd('/');

    private static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";
}
