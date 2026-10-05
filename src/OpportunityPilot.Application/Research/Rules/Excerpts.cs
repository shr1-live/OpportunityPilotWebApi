using System.Text.RegularExpressions;

namespace OpportunityPilot.Application.Research.Rules;

/// <summary>
/// The sentence (or structured-field note) that justified a verdict: the text the evidence panel shows next to a
/// breakdown row. Every excerpt is at most <see cref="MaxLength"/> characters.
/// </summary>
public static partial class Excerpts
{
    public const int MaxLength = 300;
    private const string Separator = " … ";

    /// <summary>
    /// The sentence containing <c>[start, end)</c>. A sentence ends at <c>. ! ?</c> followed by whitespace and a
    /// capital letter or digit (so ".NET", "ASP.NET", "Node.js" and "4.5" do not split it), or at a line break
    /// (blocks and list items arrive one per line).
    /// </summary>
    public static string SentenceSpan(string text, int start, int end)
    {
        start = Math.Clamp(start, 0, text.Length);
        end = Math.Clamp(end, start, text.Length);
        var s = 0;
        foreach (Match m in SentenceBoundary().Matches(text[..start]))
            s = m.Index + m.Length;
        var e = text.Length;
        var after = SentenceBoundary().Match(text, end);
        if (after.Success) e = after.Index;
        return Cut(text[s..e].Trim());
    }

    /// <summary>The sentence in which <paramref name="term"/> (or one of its aliases) first occurs; null when it does not.</summary>
    public static string? ForTerm(string? text, string term) =>
        text is not null && TextMatch.Find(text, term) is { } hit ? SentenceSpan(text, hit.Index, hit.Index + hit.Length) : null;

    /// <summary>The first matching sentence of each term, distinct, joined; null when none occurs.</summary>
    public static string? ForTerms(string? text, IEnumerable<string> terms) => Join(terms.Select(t => ForTerm(text, t)));

    /// <summary>The value came from a structured field, not from prose: "workplaceType field = remote".</summary>
    public static string Field(string name, string value) => Cut($"{name} field = {value.Trim()}");

    /// <summary>Distinct non-empty parts joined with " … " and bounded; null when there are none.</summary>
    public static string? Join(IEnumerable<string?> parts)
    {
        var distinct = parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).Distinct().ToList();
        return distinct.Count == 0 ? null : Cut(string.Join(Separator, distinct));
    }

    public static string? Bound(string? text) => text is null ? null : Cut(text);

    private static string Cut(string text) => text.Length <= MaxLength ? text : text[..(MaxLength - 1)].TrimEnd() + "…";

    [GeneratedRegex(@"(?<=[.!?])[ \t]+(?=[A-Z0-9])|[ \t]*(?:\r?\n)+[ \t]*", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceBoundary();
}
