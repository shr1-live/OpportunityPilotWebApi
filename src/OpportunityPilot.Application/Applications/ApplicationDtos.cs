using OpportunityPilot.Domain.Applications;

namespace OpportunityPilot.Application.Applications;

/// <summary>One result from the local agent. OccurredAt is when the agent acted, in UTC.</summary>
public sealed record ApplicationReportItem(
    ApplicationPlatform Platform,
    string ExternalJobId,
    string JobUrl,
    string Title,
    string Company,
    string? Location,
    ApplicationStatus Status,
    string? Detail,
    DateTime OccurredAt);

public sealed record ApplicationReportRequest(IReadOnlyList<ApplicationReportItem> Items);

public sealed record ApplicationReportResult(int Accepted);

public sealed record ApplicationDto(
    Guid Id,
    ApplicationPlatform Platform,
    string ExternalJobId,
    string JobUrl,
    string Title,
    string Company,
    string? Location,
    ApplicationStatus Status,
    string? Detail,
    DateTime OccurredAt,
    DateTime UpdatedAt);

/// <param name="Total">Matching rows after filters, before paging.</param>
public sealed record ApplicationPageDto(int Total, IReadOnlyList<ApplicationDto> Items);

public sealed record ApplicationSummaryDto(
    int Applied,
    int AppliedLast7Days,
    int NeedsManual,
    int DryRun,
    int Skipped,
    int Failed,
    DateTime? LastActivityAt);
