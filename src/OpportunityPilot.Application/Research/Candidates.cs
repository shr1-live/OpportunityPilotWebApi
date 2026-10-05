using System.Text.RegularExpressions;
using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.Application.Research;

/// <summary>One posting or company gathered from a source, before scoring.</summary>
/// <param name="Text">Description used by the rules, at most <see cref="Candidates.MaxTextLength"/> characters.</param>
/// <param name="Excerpt">What is stored as evidence (bounded to 2000 characters by the entity).</param>
/// <param name="ApplyUrl">Where the user applies, when the source gives one distinct from <paramref name="Url"/> (Lever); otherwise Url is used.</param>
public sealed record Candidate(
    Guid SourceId,
    string SourceLabel,
    string Title,
    string Organization,
    string? Location,
    string? Url,
    JobPlatform? Platform,
    string? ExternalId,
    string Text,
    string? Website,
    string? Country,
    string? Industry,
    string? EvidenceUrl,
    string Excerpt,
    IReadOnlyList<string> Links,
    string? ApplyUrl = null);

public static partial class Candidates
{
    public const int MaxTextLength = 20_000;
    public const int MinPageTextLength = 200;

    /// <summary>
    /// Job: "job:{platform}:{externalId}" when both are known, else normalised "title|organization".
    /// Customer: the website's domain when known, else the normalised name. Bounded to 400 characters.
    /// </summary>
    public static string DedupeKey(OpportunityMode mode, JobPlatform? platform, string? externalId, string title, string organization, string? website)
    {
        string key;
        if (mode == OpportunityMode.Job)
            key = platform is { } p && !string.IsNullOrWhiteSpace(externalId)
                ? $"job:{p}:{externalId.Trim()}"
                : $"{Normalise(title)}|{Normalise(organization)}";
        else
            key = Domain(website) ?? Normalise(string.IsNullOrWhiteSpace(organization) ? title : organization);
        return key.Length <= 400 ? key : key[..400];
    }

    /// <summary>Lowercase, punctuation to single spaces, trimmed.</summary>
    public static string Normalise(string? value) =>
        NonWord().Replace((value ?? string.Empty).ToLowerInvariant(), " ").Trim();

    /// <summary>Host of a website without "www.", lowercase; accepts bare domains. Null when not a usable host.</summary>
    public static string? Domain(string? website)
    {
        if (string.IsNullOrWhiteSpace(website)) return null;
        var value = website.Trim();
        if (!value.Contains("://", StringComparison.Ordinal)) value = "https://" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !uri.Host.Contains('.')) return null;
        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    public static string Bound(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= MaxTextLength ? text : text[..MaxTextLength];

    /// <summary>"Company: Acme" style lines inside fetched text, used only when the source has no structured field.</summary>
    public static string? Labelled(string text, string label)
    {
        var m = Regex.Match(text, $@"(?im)^\s*{Regex.Escape(label)}\s*:\s*(?<v>[^\r\n]{{1,200}})$", RegexOptions.None, TimeSpan.FromMilliseconds(250));
        return m.Success ? m.Groups["v"].Value.Trim() : null;
    }

    /// <summary>"Acme | Home" or "Acme - Careers" → "Acme". Page titles often carry a suffix.</summary>
    public static string? CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var first = TitleSeparator().Split(title.Trim())[0].Trim();
        return first.Length > 0 ? first : title.Trim();
    }

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonWord();

    [GeneratedRegex(@"\s+[|\-–—·:]\s+")]
    private static partial Regex TitleSeparator();
}
