using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.Application.Campaigns;

public sealed record LastJobDto(Guid Id, ResearchJobState State, ResearchStage Stage, DateTime? FinishedAt);

public sealed record CampaignSummaryDto(
    Guid Id,
    Guid ProfileId,
    OpportunityMode Mode,
    string Name,
    string Goal,
    int ResultLimit,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int SourceCount,
    int OpportunityCount,
    LastJobDto? LastJob);

/// <summary>CampaignSummary plus the editable criteria and the normalised weights.</summary>
public sealed record CampaignDto(
    Guid Id,
    Guid ProfileId,
    OpportunityMode Mode,
    string Name,
    string Goal,
    int ResultLimit,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int SourceCount,
    int OpportunityCount,
    LastJobDto? LastJob,
    CampaignCriteria Criteria,
    IReadOnlyDictionary<string, int> Weights);

/// <param name="Weights">Optional; the mode's defaults when omitted. Normalised to sum 100.</param>
public sealed record CreateCampaignRequest(
    Guid ProfileId,
    OpportunityMode Mode,
    string Name,
    string? Goal,
    CampaignCriteria? Criteria,
    IReadOnlyDictionary<string, double>? Weights,
    int? ResultLimit);

/// <summary>ExpectedVersion must match the stored version, otherwise the edit is rejected with 409.</summary>
public sealed record UpdateCampaignRequest(
    string Name,
    string? Goal,
    CampaignCriteria? Criteria,
    IReadOnlyDictionary<string, double>? Weights,
    int? ResultLimit,
    int ExpectedVersion);
