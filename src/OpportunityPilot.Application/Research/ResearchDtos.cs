using System.Text.Json;
using System.Text.Json.Serialization;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.Application.Research;

/// <param name="Fetched">Raw items gathered from sources (postings, rows, feed entries, pages) before limits and dedupe.</param>
/// <param name="Candidates">Items extracted and scored in this run.</param>
public sealed record ResearchCounts(
    int Sources = 0,
    int SourcesDone = 0,
    int SourcesFailed = 0,
    int Fetched = 0,
    int Candidates = 0,
    int Qualified = 0,
    int NeedsVerification = 0,
    int Excluded = 0)
{
    public string ToJson() => JsonSerializer.Serialize(this, JsonSerializerOptions.Web);

    public static ResearchCounts FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<ResearchCounts>(json, JsonSerializerOptions.Web) ?? new(); }
        catch (JsonException) { return new(); }
    }
}

public sealed record ResearchEventDto(DateTime At, ResearchStage Stage, EventLevel Level, string Message);

/// <param name="Events">Latest 100, newest first. Omitted from lists.</param>
public sealed record ResearchJobDto(
    Guid Id,
    Guid CampaignId,
    ResearchJobState State,
    ResearchStage Stage,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? SafeError,
    ResearchCounts Counts,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ResearchEventDto>? Events);

public sealed record QueuedResearchDto(Guid JobId);
