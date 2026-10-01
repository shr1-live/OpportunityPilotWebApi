using System.Text.RegularExpressions;

namespace OpportunityPilot.Application.Sources;

/// <param name="Title">First non-empty line: the job title (Job) or company name (Customer).</param>
/// <param name="Description">Every line that is not the title or a recognised "Label: value" line.</param>
/// <param name="Raw">The whole block, trimmed: what the evidence excerpt is cut from.</param>
public sealed record PastedEntry(
    string Title, string? Company, string? Location, string? Website, string? Url,
    string? Country, string? Industry, string Description, string Raw);

/// <summary>
/// Pasted text holds one or more postings/companies separated by a line that is exactly "---".
/// Lines like "Company: X", "Location: Y", "Website: Z", "URL: …" (plus "Country:" and "Industry:") set fields.
/// </summary>
public static partial class PasteParser
{
    public static IReadOnlyList<PastedEntry> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var entries = new List<PastedEntry>();
        var block = new List<string>();
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (line.Trim() == "---")
            {
                AddBlock(block, entries);
                block.Clear();
            }
            else block.Add(line);
        }
        AddBlock(block, entries);
        return entries;
    }

    private static void AddBlock(List<string> lines, List<PastedEntry> entries)
    {
        string? title = null, company = null, location = null, website = null, url = null, country = null, industry = null;
        var description = new List<string>();
        foreach (var original in lines)
        {
            var line = original.Trim();
            if (line.Length == 0)
            {
                if (title is not null && description.Count > 0 && description[^1].Length > 0) description.Add(string.Empty);
                continue;
            }
            if (title is null)
            {
                title = line;
                continue;
            }

            var label = Label().Match(line);
            if (label.Success)
            {
                var value = label.Groups["value"].Value.Trim();
                switch (label.Groups["key"].Value.ToLowerInvariant())
                {
                    case "company": company ??= value; continue;
                    case "location": location ??= value; continue;
                    case "website": website ??= value; continue;
                    case "url": url ??= value; continue;
                    case "country": country ??= value; continue;
                    case "industry": industry ??= value; continue;
                }
            }
            description.Add(line);
        }

        if (title is null) return;
        var raw = string.Join('\n', lines).Trim();
        entries.Add(new PastedEntry(title, Blank(company), Blank(location), Blank(website), Blank(url), Blank(country),
            Blank(industry), string.Join('\n', description).Trim(), raw));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    [GeneratedRegex(@"^(?<key>company|location|website|url|country|industry)\s*:\s*(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Label();
}
