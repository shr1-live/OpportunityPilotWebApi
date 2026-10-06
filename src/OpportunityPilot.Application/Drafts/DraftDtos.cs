using OpportunityPilot.Domain.Drafts;

namespace OpportunityPilot.Application.Drafts;

public sealed record DraftClaimDto(string Text, string Basis, Guid? EvidenceId);

public sealed record DraftDto(
    Guid Id, Guid OpportunityId, DraftChannel Channel, string? Recipient, bool RecipientVerified, string? Subject,
    string Body, int Version, DraftState State, int? ApprovedVersion, DateTime? ApprovedAt, DraftSource Source,
    string? FallbackReason, IReadOnlyList<DraftClaimDto> Claims, bool SendReady, IReadOnlyList<string> SendBlockers,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CreateDraftRequest(DraftChannel Channel, string? Recipient);
public sealed record UpdateDraftRequest(string? Recipient, string? Subject, string Body, int ExpectedVersion);
public sealed record ApproveDraftRequest(int Version);
public sealed record DraftListItemDto(
    Guid Id, Guid OpportunityId, string OpportunityTitle, string Organization, DraftChannel Channel,
    string? Recipient, DraftState State, int Version, DateTime UpdatedAt);
public sealed record DraftPageDto(int Total, IReadOnlyList<DraftListItemDto> Items);
public sealed record BatchApproveDraftItem(Guid Id, int Version);
public sealed record BatchApproveDraftRequest(IReadOnlyList<BatchApproveDraftItem> Items);
public sealed record BatchApproveDraftResult(Guid Id, bool Approved, string? Reason, DraftDto? Draft);
