using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace OpportunityPilot.Application.Research.Rules;

/// <summary>
/// Case-insensitive term matching on word boundaries, where a boundary is any non-letter/digit character, so
/// "c#", ".net" and "node.js" work. A few common spellings are treated as the same skill.
/// </summary>
public static class TextMatch
{
    // Versioned with the rules: changing these changes scores.
    private static readonly string[][] AliasGroups =
    [
        ["c#", "csharp", "c sharp"],
        [".net", "dotnet", "asp.net"],
        ["javascript", "js"],
        ["typescript", "ts"]
    ];

    private static readonly ConcurrentDictionary<string, Regex> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Variants(string term)
    {
        var key = term.Trim();
        var group = AliasGroups.FirstOrDefault(g => g.Contains(key, StringComparer.OrdinalIgnoreCase));
        return group is null ? [key] : [key, .. group.Where(v => !v.Equals(key, StringComparison.OrdinalIgnoreCase))];
    }

    public static bool Contains(string? text, string term)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(term)) return false;
        return Variants(term).Any(v => For(v).IsMatch(text));
    }

    /// <summary>The configured terms (as the user wrote them) that occur in any of the texts.</summary>
    public static List<string> Found(IEnumerable<string> terms, params string?[] texts) =>
        terms.Where(t => texts.Any(x => Contains(x, t))).ToList();

    private static Regex For(string variant) => Cache.GetOrAdd(variant, v =>
    {
        var body = string.Join(@"\s+", v.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape));
        return new Regex($@"(?<![\p{{L}}\p{{N}}]){body}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    });
}
