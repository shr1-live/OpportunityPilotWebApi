using System.Globalization;
using System.Text.Json;
using OpportunityPilot.Domain.Opportunities;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.Application.Research.Boards;

/// <summary>One row of a Workday search: <c>{ title, externalPath, locationsText, postedOn, bulletFields: [reqId] }</c>.</summary>
public sealed record WorkdayPosting(string ExternalPath, string Title, string? Location, string? RequisitionId);

/// <summary>The job detail's <c>jobPostingInfo</c>: description (HTML), location, start date, time type, public URL.</summary>
public sealed record WorkdayDetail(string? Description, string? Location, DateTime? StartDate, string? TimeType, string? ExternalUrl, string? RequisitionId);

/// <summary>
/// Pure JSON mapping for Workday's public careers-site endpoints (<c>/wday/cxs/{tenant}/{site}/jobs</c> and the job
/// detail under the same root). Tolerant like the other boards: a posting without a path or title is dropped and a
/// body that is not the expected shape returns null. Workday's "Posted 30+ Days Ago" text is never turned into a date.
/// </summary>
public static class WorkdayMapping
{
    /// <summary>Workday serves at most 20 postings per search request.</summary>
    public const int PageSize = 20;

    public static string SearchBody(string? searchText, int offset = 0) =>
        JsonSerializer.Serialize(new { appliedFacets = new { }, limit = PageSize, offset, searchText = searchText ?? "" });

    public static (IReadOnlyList<WorkdayPosting> Postings, int Total)? Postings(string? json)
    {
        using var document = Parse(json);
        if (document?.RootElement is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("jobPostings", out var rows) || rows.ValueKind != JsonValueKind.Array) return null;
        var total = root.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt32(out var n) ? n : rows.GetArrayLength();
        var result = new List<WorkdayPosting>();
        foreach (var row in rows.EnumerateArray())
        {
            var path = Text(row, "externalPath");
            var title = Text(row, "title");
            if (path is null || title is null || !path.StartsWith("/job/", StringComparison.Ordinal)) continue;
            string? reqId = null;
            if (row.TryGetProperty("bulletFields", out var bullets) && bullets.ValueKind == JsonValueKind.Array)
                reqId = bullets.EnumerateArray().Where(b => b.ValueKind == JsonValueKind.String).Select(b => b.GetString()).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
            result.Add(new WorkdayPosting(path, title, Text(row, "locationsText"), reqId));
        }
        return (result, total);
    }

    public static WorkdayDetail? Detail(string? json, IContentParser parser)
    {
        using var document = Parse(json);
        if (document?.RootElement is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("jobPostingInfo", out var info) || info.ValueKind != JsonValueKind.Object) return null;
        var html = Text(info, "jobDescription");
        var description = html is null ? null : parser.ParseHtml(html, null).Text;
        var start = DateTime.TryParseExact(Text(info, "startDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : (DateTime?)null;
        return new WorkdayDetail(string.IsNullOrWhiteSpace(description) ? null : description.Trim(), Text(info, "location"), start,
            Text(info, "timeType"), Text(info, "externalUrl"), Text(info, "jobReqId"));
    }

    public static Candidate Candidate(Source source, WorkdayBoard board, WorkdayPosting posting, WorkdayDetail? detail)
    {
        var url = OnBoardHost(detail?.ExternalUrl, board) ?? $"{board.SiteUrl}{posting.ExternalPath}";
        var location = detail?.Location ?? posting.Location;
        var remote = location?.Contains("remote", StringComparison.OrdinalIgnoreCase) == true ? "remote" : null;
        var externalId = detail?.RequisitionId ?? posting.RequisitionId ?? posting.ExternalPath;
        return BoardMapping.BuildCandidate(source, JobPlatform.Workday, externalId, posting.Title, BoardMapping.TitleCase(board.Tenant),
            location, remote, url, url, null, detail?.Description, detail?.StartDate);
    }

    /// <summary>Workday's own job link, but only on the board's host; anything else falls back to the derived link.</summary>
    private static string? OnBoardHost(string? url, WorkdayBoard board) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps &&
        u.Host.Equals(board.Host, StringComparison.OrdinalIgnoreCase) ? u.ToString() : null;

    private static JsonDocument? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json); } catch (JsonException) { return null; }
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim() : null;
}
