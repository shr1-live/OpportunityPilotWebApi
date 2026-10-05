using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Application.Research.Rules;
using OpportunityPilot.Domain.Opportunities;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.Application.Research.Boards;

/// <summary>One job from a Greenhouse board listing (no description: the list endpoint is read without content).</summary>
public sealed record GreenhouseJob(string Id, string Title, string? Url, string? Location, string? Company, DateTime? UpdatedAt);

/// <summary>
/// Pure JSON → <see cref="Candidate"/> mapping for the public job-board APIs. Every reader is tolerant: missing or
/// null fields are skipped, a posting without an id or title is dropped, and a body that is not the documented shape
/// returns null (the caller records a safe "could not be read" reason). Descriptions that arrive as HTML are reduced
/// to text with <see cref="IContentParser"/> (scripting off, no external resources).
/// </summary>
public static class BoardMapping
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "the", "for", "with", "of", "in", "at", "to", "or", "on", "by"
    };

    // ---------- Greenhouse ----------

    /// <summary><c>{ jobs: [{ id, title, absolute_url, location: { name }, updated_at, company_name }] }</c>.</summary>
    public static IReadOnlyList<GreenhouseJob>? GreenhouseJobs(string? json)
    {
        using var document = Parse(json);
        if (document?.RootElement is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
            return null;

        var result = new List<GreenhouseJob>();
        foreach (var job in jobs.EnumerateArray())
        {
            var id = Text(job, "id");
            var title = Text(job, "title");
            if (id is null || title is null) continue;
            result.Add(new GreenhouseJob(id, title, HttpUrl(Text(job, "absolute_url")), Text(Child(job, "location"), "name"),
                Text(job, "company_name"), Date(Text(job, "updated_at"))));
        }
        return result;
    }

    /// <summary>The job detail's <c>content</c>: HTML that Greenhouse sends entity-escaped, so it is unescaped first, then reduced to text.</summary>
    public static string? GreenhouseDescription(string? json, IContentParser parser)
    {
        using var document = Parse(json);
        if (document?.RootElement is not { ValueKind: JsonValueKind.Object } root) return null;
        var content = Text(root, "content");
        return content is null ? null : HtmlToText(WebUtility.HtmlDecode(content), parser);
    }

    public static Candidate GreenhouseCandidate(Source source, string token, GreenhouseJob job, string? description)
    {
        var organization = job.Company ?? token;
        return Build(source, JobPlatform.Greenhouse, job.Id, job.Title, organization, job.Location, null, job.Url, job.Url, null, description,
            job.UpdatedAt);
    }

    // ---------- Lever ----------

    /// <summary>
    /// <c>[{ id, text, hostedUrl, applyUrl, categories: { location }, workplaceType, descriptionPlain, lists: [{ text, content }], country }]</c>.
    /// </summary>
    public static IReadOnlyList<Candidate>? LeverCandidates(string? json, Source source, string slug, IContentParser parser)
    {
        using var document = Parse(json);
        if (document?.RootElement is not { ValueKind: JsonValueKind.Array } postings) return null;

        var organization = TitleCase(slug);
        var result = new List<Candidate>();
        foreach (var p in postings.EnumerateArray())
        {
            var id = Text(p, "id");
            var title = Text(p, "text");
            if (id is null || title is null) continue;

            var description = new StringBuilder(Text(p, "descriptionPlain") ?? string.Empty);
            if (Child(p, "lists") is { ValueKind: JsonValueKind.Array } lists)
            {
                foreach (var list in lists.EnumerateArray())
                {
                    var heading = Text(list, "text");
                    var items = Text(list, "content") is { } html ? HtmlToText(html, parser) : null;
                    if (heading is null && items is null) continue;
                    description.Append("\n\n").Append(heading).Append(heading is not null && items is not null ? "\n" : "").Append(items);
                }
            }
            var hostedUrl = HttpUrl(Text(p, "hostedUrl"));
            var workplaceType = Text(p, "workplaceType");
            result.Add(Build(source, JobPlatform.Lever, id, title, organization, Text(Child(p, "categories"), "location"),
                workplaceType, hostedUrl, HttpUrl(Text(p, "applyUrl")) ?? hostedUrl, Text(p, "country"),
                description.ToString().Trim(), EpochMilliseconds(p, "createdAt")));
        }
        return result;
    }

    // ---------- Adzuna ----------

    /// <summary><c>{ results: [{ id, title, company: { display_name }, location: { display_name }, redirect_url, description }] }</c>.</summary>
    public static IReadOnlyList<Candidate>? AdzunaCandidates(string? json, Source source, IContentParser parser)
    {
        using var document = Parse(json);
        if (document?.RootElement is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return null;

        var result = new List<Candidate>();
        foreach (var r in results.EnumerateArray())
        {
            var id = Text(r, "id");
            // Adzuna marks the search words with <strong> in titles and snippets.
            var title = Text(r, "title") is { } t ? HtmlToText(t, parser) : null;
            if (id is null || string.IsNullOrEmpty(title)) continue;
            var url = HttpUrl(Text(r, "redirect_url"));
            var description = Text(r, "description") is { } d ? HtmlToText(d, parser) : null;
            result.Add(Build(source, JobPlatform.Adzuna, id, title, Text(Child(r, "company"), "display_name") ?? string.Empty,
                Text(Child(r, "location"), "display_name"), null, url, url, null, description));
        }
        return result;
    }

    // ---------- Title pre-filter ----------

    /// <summary>
    /// The terms a job title is matched against before descriptions are fetched: each campaign keyword phrase, each
    /// word of those phrases (common short words dropped) and each required skill. Empty when none are configured.
    /// </summary>
    public static IReadOnlyList<string> TitleTerms(CampaignCriteria criteria)
    {
        var terms = new List<string>();
        void Add(string term)
        {
            term = term.Trim();
            if (term.Length > 0 && !terms.Contains(term, StringComparer.OrdinalIgnoreCase)) terms.Add(term);
        }
        foreach (var keyword in criteria.Keywords)
        {
            Add(keyword);
            foreach (var word in keyword.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                if (word.Length > 1 && !StopWords.Contains(word)) Add(word);
        }
        foreach (var skill in criteria.RequiredSkills) Add(skill);
        return terms;
    }

    /// <summary>
    /// Keeps jobs whose title contains any term (word-boundary match with the skill aliases of the rules); keeps all
    /// when there are no terms. The best matches (most terms) come first, then the most recently updated, so a small
    /// fetch budget is spent on the likeliest jobs.
    /// </summary>
    public static List<GreenhouseJob> Prefilter(IReadOnlyList<GreenhouseJob> jobs, IReadOnlyList<string> terms) =>
        jobs.Select((job, index) => (job, index, hits: terms.Count == 0 ? 0 : terms.Count(t => TextMatch.Contains(job.Title, t))))
            .Where(x => terms.Count == 0 || x.hits > 0)
            .OrderByDescending(x => x.hits)
            .ThenByDescending(x => x.job.UpdatedAt ?? DateTime.MinValue)
            .ThenBy(x => x.index)
            .Select(x => x.job)
            .ToList();

    // ---------- helpers ----------

    private static Candidate Build(Source source, JobPlatform platform, string externalId, string title, string organization,
        string? location, string? workplaceType, string? url, string? applyUrl, string? country, string? description,
        DateTime? postedAt = null)
    {
        var workplace = WorkplaceHint(workplaceType);
        var text = Candidates.Bound(string.Join("\n", new[] { workplace is null ? null : $"Workplace: {workplace}", description }
            .Where(x => !string.IsNullOrWhiteSpace(x))));
        var lines = new List<string> { title };
        if (organization.Length > 0) lines.Add($"Company: {organization}");
        if (location is not null) lines.Add($"Location: {location}");
        if (workplace is not null) lines.Add($"Workplace: {workplace}");
        if (url is not null) lines.Add($"URL: {url}");
        if (!string.IsNullOrWhiteSpace(description)) lines.Add("\n" + description);
        return new Candidate(source.Id, source.Label, title, organization, location, url, platform, externalId, text, null, country, null,
            url, string.Join('\n', lines), [], applyUrl, workplaceType, postedAt);
    }

    /// <summary>Lever's <c>workplaceType</c> in words the work-mode rule recognises; unknown values give no hint.</summary>
    private static string? WorkplaceHint(string? workplaceType) => workplaceType?.Trim().ToLowerInvariant() switch
    {
        "remote" => "Remote",
        "hybrid" => "Hybrid",
        "on-site" or "onsite" => "On-site",
        _ => null
    };

    private static string HtmlToText(string html, IContentParser parser) =>
        html.Contains('<') || html.Contains('&') ? parser.ParseHtml(html, null).Text : html.Trim();

    /// <summary>"acme-corp" → "Acme Corp".</summary>
    public static string TitleCase(string slug) =>
        string.Join(' ', slug.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    private static JsonDocument? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 }); }
        catch (JsonException) { return null; }
    }

    private static JsonElement? Child(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var child) ? child : null;

    /// <summary>A string, or a number's literal text (ids are numbers on Greenhouse, strings on Adzuna); blank → null.</summary>
    private static string? Text(JsonElement? element, string name) => Child(element, name) switch
    {
        { ValueKind: JsonValueKind.String } s when !string.IsNullOrWhiteSpace(s.GetString()) => s.GetString()!.Trim(),
        { ValueKind: JsonValueKind.Number } n => n.GetRawText(),
        _ => null
    };

    private static string? HttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? url : null;

    private static DateTime? Date(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at.UtcDateTime : null;

    private static DateTime? EpochMilliseconds(JsonElement element, string name) => Child(element, name) switch
    {
        { ValueKind: JsonValueKind.Number } n when n.TryGetInt64(out var value) =>
            DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime,
        _ => null
    };
}
