using OpportunityPilot.Application.Research.Rules;
using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.Application.Opportunities;

public sealed record OpportunitySummaryDto(
    Guid Id,
    Guid CampaignId,
    OpportunityMode Mode,
    string Title,
    string Organization,
    string? Location,
    string? Url,
    string? ApplyUrl,
    JobPlatform? Platform,
    int Score,
    int Coverage,
    FilterOutcome Outcome,
    string? OutcomeReason,
    OpportunityStatus Status,
    int GapsCount,
    DateTime UpdatedAt);

/// <param name="Total">Matching rows after filters, before paging.</param>
public sealed record OpportunityPageDto(int Total, IReadOnlyList<OpportunitySummaryDto> Items);

public sealed record EvidenceDto(Guid Id, Guid SourceId, string? SourceLabel, string? Url, DateTime RetrievedAt, string Excerpt, string ExtractionMethod);

public sealed record ActivityDto(string Kind, DateTime OccurredAt, string Detail);

/// <summary>OpportunitySummary plus everything behind the score.</summary>
public sealed record OpportunityDetailDto(
    Guid Id,
    Guid CampaignId,
    OpportunityMode Mode,
    string Title,
    string Organization,
    string? Location,
    string? Url,
    string? ApplyUrl,
    JobPlatform? Platform,
    int Score,
    int Coverage,
    FilterOutcome Outcome,
    string? OutcomeReason,
    OpportunityStatus Status,
    int GapsCount,
    DateTime UpdatedAt,
    string? Description,
    int Version,
    IReadOnlyList<BreakdownRow> Breakdown,
    IReadOnlyList<FactRow> Facts,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<EvidenceDto> Evidence,
    IReadOnlyList<ActivityDto> Activities);

public sealed record UpdateOpportunityStatusRequest(OpportunityStatus Status);

public sealed record CsvExport(string FileName, string Content);
