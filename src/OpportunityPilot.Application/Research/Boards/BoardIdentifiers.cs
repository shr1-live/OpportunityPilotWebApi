using System.Text.RegularExpressions;

namespace OpportunityPilot.Application.Research.Boards;

/// <summary>
/// Turns what a user types for a job-board source into the identifier the public API needs: a Greenhouse board token
/// or a Lever company slug, both <c>[a-z0-9-]{1,100}</c>. Accepts the bare identifier or a board URL on the board's
/// own host; anything else is refused (null), so a source can never point the fetcher at another host.
/// </summary>
public static partial class BoardIdentifiers
{
    public const int MaxLength = 100;

    private static readonly string[] GreenhouseHosts = ["boards.greenhouse.io", "job-boards.greenhouse.io"];
    private static readonly string[] LeverHosts = ["jobs.lever.co"];

    /// <summary>
    /// <c>stripe</c>, <c>https://boards.greenhouse.io/stripe</c>, <c>job-boards.greenhouse.io/stripe/jobs/123</c> and
    /// <c>boards.greenhouse.io/embed/job_board?for=stripe</c> all give <c>stripe</c>.
    /// </summary>
    public static string? Greenhouse(string? input) => Normalise(input, GreenhouseHosts, embedQuery: "for");

    /// <summary><c>leverdemo</c> and <c>https://jobs.lever.co/leverdemo/&lt;posting id&gt;</c> give <c>leverdemo</c>.</summary>
    public static string? Lever(string? input) => Normalise(input, LeverHosts, embedQuery: null);

    private static string? Normalise(string? input, string[] hosts, string? embedQuery)
    {
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        if (Identifier().IsMatch(value)) return Valid(value);

        var candidate = value.Contains("://", StringComparison.Ordinal) ? value : "https://" + value;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return null;
        if (!string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return null;
        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];
        if (!hosts.Contains(host)) return null;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (embedQuery is not null && segments is ["embed", ..])
            return Valid(QueryValue(uri.Query, embedQuery));
        return segments.Length == 0 ? null : Valid(Uri.UnescapeDataString(segments[0]));
    }

    private static string? Valid(string? value)
    {
        var lower = value?.Trim().ToLowerInvariant();
        return lower is { Length: > 0 and <= MaxLength } && Identifier().IsMatch(lower) ? lower : null;
    }

    private static string? QueryValue(string query, string name)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals(name, StringComparison.OrdinalIgnoreCase)) return Uri.UnescapeDataString(parts[1]);
        }
        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,100}$")]
    private static partial Regex Identifier();
}
