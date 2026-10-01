using OpportunityPilot.Domain.Applications;

namespace OpportunityPilot.Application.Applications;

/// <summary>One result from the local agent. OccurredAt is when the agent acted, in UTC.</summary>
/// <param name="OpportunityId">Set when the job came from the research shortlist; an Applied report then moves that opportunity to Applied.</param>
public sealed record ApplicationReportItem(
    ApplicationPlatform Platform,
    string ExternalJobId,
    string JobUrl,
    string Title,
    string Company,
    string? Location,
    ApplicationStatus Status,
    string? Detail,
    DateTime OccurredAt,
    Guid? OpportunityId = null);

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
