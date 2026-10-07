using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.Application.Approvals;

/// <summary>Who submits the application once approved: the desktop agent (LinkedIn, Naukri) or the user via the apply URL.</summary>
public enum AppliesVia
{
    Agent,
    You
}

/// <summary>One Suggested opportunity waiting for the user's decision.</summary>
public sealed record ApprovalItem(
    Guid OpportunityId,
    Guid CampaignId,
    string CampaignName,
    string Title,
    string Organization,
    string? Location,
    JobPlatform? Platform,
    string? ApplyUrl,
    int Score,
    int Coverage,
    string? OutcomeReason,
    AppliesVia AppliesVia);

/// <param name="Total">Suggested opportunities matching the filter, before paging.</param>
public sealed record ApprovalPageDto(int Total, IReadOnlyList<ApprovalItem> Items);

/// <summary>Ids to approve (→ Shortlisted) and to reject (→ Dismissed); at most 200 in total, no id in both lists.</summary>
public sealed record DecideApprovalsRequest(IReadOnlyList<Guid>? Approve, IReadOnlyList<Guid>? Reject);

/// <param name="Skipped">Ids that are not the caller's, do not exist, or are no longer Suggested. Not errors.</param>
public sealed record DecideApprovalsResult(int Approved, int Rejected, int Skipped);
public sealed record DecideAllApprovalsRequest(bool Approve, Guid? CampaignId);
