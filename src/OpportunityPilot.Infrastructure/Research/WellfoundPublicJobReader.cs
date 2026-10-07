using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using OpportunityPilot.Application.Research;
using OpportunityPilot.Application.Wellfound;

namespace OpportunityPilot.Infrastructure.Research;

/// <summary>Reads only the anonymous public Wellfound jobs landing page. It never uses cookies or authenticated pages.</summary>
public sealed partial class WellfoundPublicJobReader(IWebFetcher fetcher, TimeProvider clock) : IWellfoundPublicJobReader
{
    public const string PublicJobsUrl = "https://wellfound.com/jobs";
    public const int MaxJobs = 100;

    public async Task<WellfoundPublicReadResult> ReadAsync(CancellationToken ct)
    {
        var observedAt = clock.GetUtcNow().UtcDateTime;
        var fetched = await fetcher.FetchAsync(PublicJobsUrl, ct);
        if (!fetched.Ok || string.IsNullOrWhiteSpace(fetched.Content))
            return new(false, [], fetched.FailureReason ?? "Wellfound returned no public job content.", observedAt);

        var document = new HtmlParser(new HtmlParserOptions { IsScripting = false }).ParseDocument(fetched.Content);
        var jobs = new List<WellfoundPublicJob>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in document.QuerySelectorAll("a[href^='/jobs/']"))
        {
            var href = link.GetAttribute("href")?.Trim();
            if (href is null || href.StartsWith("/jobs/signup", StringComparison.OrdinalIgnoreCase)) continue;
            var idMatch = JobId().Match(href);
            if (!idMatch.Success || !seen.Add(idMatch.Groups[1].Value)) continue;

            var card = FindCard(link);
            var detail = card?.QuerySelector("div.text-sm");
            var spans = detail?.Children.Where(x => x.LocalName == "span").ToArray() ?? [];
            var company = Clean(spans.FirstOrDefault()?.TextContent).TrimEnd('•').Trim();
            var facts = Clean(spans.Skip(1).FirstOrDefault()?.TextContent);
            if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(link.TextContent)) continue;

            var parsed = ParseFacts(facts, observedAt);
            var applyUrl = new Uri(new Uri(PublicJobsUrl), href).ToString();
            var evidence = JsonSerializer.Serialize(new
            {
                source = PublicJobsUrl,
                observedAt,
                rawCardText = Clean(detail?.TextContent),
                scope = "anonymous-public-page"
            });
            jobs.Add(new(idMatch.Groups[1].Value, Clean(link.TextContent), company, applyUrl,
                parsed.Location, parsed.RemoteType, parsed.SalaryMin, parsed.SalaryMax, parsed.Currency,
                parsed.EquityMin, parsed.EquityMax, parsed.PostedAt, evidence));
            if (jobs.Count >= MaxJobs) break;
        }

        return jobs.Count == 0
            ? new(false, [], "Wellfound's public page returned no recognizable job cards.", observedAt)
            : new(true, jobs, null, observedAt);
    }

    private static IElement? FindCard(IElement link)
    {
        for (var node = link.ParentElement; node is not null; node = node.ParentElement)
        {
            if (node.QuerySelector("button[data-test='JobApplicationApplyButton']") is not null) return node;
            if (node.LocalName == "body") break;
        }
        return link.ParentElement?.ParentElement;
    }

    private static ParsedFacts ParseFacts(string text, DateTime observedAt)
    {
        var parts = text.Split('•', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Clean).Where(x => x.Length > 0 && !MoreLocations().IsMatch(x)).ToList();
        var remote = parts.FirstOrDefault(x => WorkMode().IsMatch(x));
        var salaryPart = parts.FirstOrDefault(x => MoneyRange().IsMatch(x));
        var equityPart = parts.FirstOrDefault(x => x.Contains('%') || x.Equals("No equity", StringComparison.OrdinalIgnoreCase));
        var postedPart = parts.FirstOrDefault(x => Posted().IsMatch(x));
        var location = parts.FirstOrDefault(x => x != remote && x != salaryPart && x != equityPart && x != postedPart);
        var salary = ParseMoney(salaryPart);
        var equity = ParseEquity(equityPart);
        return new(location, remote, salary.Min, salary.Max, salary.Currency, equity.Min, equity.Max,
            ParsePosted(postedPart, observedAt));
    }

    private static (decimal? Min, decimal? Max, string? Currency) ParseMoney(string? value)
    {
        if (value is null) return (null, null, null);
        var matches = Amount().Matches(value);
        if (matches.Count == 0) return (null, null, null);
        var currency = value.Contains("CAD", StringComparison.OrdinalIgnoreCase) ? "CAD" :
            value.Contains('€') ? "EUR" : value.Contains('₹') ? "INR" : value.Contains('₱') ? "PHP" : "USD";
        decimal Read(Match match)
        {
            if (!decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return 0;
            return match.Groups[2].Value.ToUpperInvariant() switch { "K" => number * 1_000, "L" => number * 100_000, "M" => number * 1_000_000, _ => number };
        }
        return (Read(matches[0]), Read(matches.Count > 1 ? matches[1] : matches[0]), currency);
    }

    private static (decimal? Min, decimal? Max) ParseEquity(string? value)
    {
        if (value is null) return (null, null);
        if (value.Equals("No equity", StringComparison.OrdinalIgnoreCase)) return (0, 0);
        var matches = Percent().Matches(value);
        decimal? Read(int index) => matches.Count > index && decimal.TryParse(matches[index].Groups[1].Value,
            NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;
        return (Read(0), Read(matches.Count > 1 ? 1 : 0));
    }

    private static DateTime? ParsePosted(string? value, DateTime observedAt)
    {
        if (value is null) return null;
        if (value.Equals("today", StringComparison.OrdinalIgnoreCase)) return observedAt;
        if (value.Equals("yesterday", StringComparison.OrdinalIgnoreCase)) return observedAt.AddDays(-1);
        var match = Ago().Match(value);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var count)) return null;
        return observedAt.AddDays(-count * (match.Groups[2].Value.StartsWith("week", StringComparison.OrdinalIgnoreCase) ? 7 : 1));
    }

    private static string Clean(string? value) => Whitespace().Replace(System.Net.WebUtility.HtmlDecode(value ?? ""), " ").Trim();

    private sealed record ParsedFacts(string? Location, string? RemoteType, decimal? SalaryMin, decimal? SalaryMax,
        string? Currency, decimal? EquityMin, decimal? EquityMax, DateTime? PostedAt);

    [GeneratedRegex("^/jobs/(\\d+)-", RegexOptions.IgnoreCase)] private static partial Regex JobId();
    [GeneratedRegex("^(remote|remote only|in office|onsite or remote|hybrid)$", RegexOptions.IgnoreCase)] private static partial Regex WorkMode();
    [GeneratedRegex("[\\$€₹₱]\\s*[\\d,.]+\\s*[kKlLmM]?")] private static partial Regex MoneyRange();
    [GeneratedRegex("[\\$€₹₱]?\\s*([\\d,.]+)\\s*([kKlLmM]?)")] private static partial Regex Amount();
    [GeneratedRegex("([\\d.]+)%")] private static partial Regex Percent();
    [GeneratedRegex("^(today|yesterday|\\d+ (day|days|week|weeks) ago)$", RegexOptions.IgnoreCase)] private static partial Regex Posted();
    [GeneratedRegex("^(\\d+) (day|days|week|weeks) ago$", RegexOptions.IgnoreCase)] private static partial Regex Ago();
    [GeneratedRegex("^\\+\\s*\\d+\\s*more$", RegexOptions.IgnoreCase)] private static partial Regex MoreLocations();
    [GeneratedRegex("\\s+")] private static partial Regex Whitespace();
}
