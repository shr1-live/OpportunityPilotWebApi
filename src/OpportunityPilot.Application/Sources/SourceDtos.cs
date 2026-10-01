using OpportunityPilot.Domain.Opportunities;
using OpportunityPilot.Domain.Research;

namespace OpportunityPilot.Application.Sources;

public sealed record SourceDto(
    Guid Id,
    Guid CampaignId,
    SourceKind Kind,
    string Label,
    string? Url,
    JobPlatform? Platform,
    string? PermissionNote,
    SourceStatus Status,
    DateTime? LastFetchedAt,
    string? SafeError,
    int ItemCount,
    int TextLength,
    DateTime CreatedAt);

/// <summary>Only Paste, Url and Feed are created here; Csv comes from an import commit and Agent from the desktop agent.</summary>
public sealed record CreateSourceRequest(SourceKind Kind, string? Label, string? Url, string? Text, string? PermissionNote);
