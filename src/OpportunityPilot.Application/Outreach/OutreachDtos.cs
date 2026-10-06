using OpportunityPilot.Domain.Outreach;

namespace OpportunityPilot.Application.Outreach;

public sealed record SuppressionDto(Guid Id, string Recipient, string Reason, DateTime CreatedAt);
public sealed record CreateSuppressionRequest(string Recipient, string? Reason);

public sealed record NextActionDto(
    Guid Id, Guid OpportunityId, string OpportunityTitle, string Organization, NextActionKind Kind,
    string Note, DateTime DueAt, string TimeZone, NextActionState State, bool Overdue,
    DateTime CreatedAt, DateTime? CompletedAt);
public sealed record CreateNextActionRequest(NextActionKind Kind, string? Note, DateTime DueAt, string TimeZone);
public sealed record UpdateNextActionRequest(NextActionState? State, DateTime? DueAt);

public sealed record CreateActivityRequest(string Kind, string? Detail, DateTime? OccurredAt);
