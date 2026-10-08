using OpportunityPilot.Domain.Staffing;

namespace OpportunityPilot.Application.Staffing;

public sealed record StaffingMessageDto(
    Guid Id, Guid DealId, Guid? ContactId, MessageDirection Direction, MessageChannel Channel, string Counterpart, string? Subject,
    string Body, MessageState State, string? Receipt, DateTime? OccurredAt, ReplyIntent Intent, bool Suppressed, int Version, DateTime CreatedAt);

public sealed record StaffingMeetingDto(
    Guid Id, Guid DealId, string Title, DateTime StartsAt, string TimeZone, int DurationMinutes, string Invitees, string? Agenda,
    string? Link, MeetingState State, string? Receipt, string? Outcome, int Version, DateTime? UpdatedAt);

public sealed record StaffingConversationDto(IReadOnlyList<StaffingMessageDto> Messages, IReadOnlyList<StaffingMeetingDto> Meetings);

public sealed record DraftMessageRequest(Guid? ContactId, MessageChannel Channel, string Recipient, string? Subject, string Body);
public sealed record EditMessageRequest(string Recipient, string? Subject, string Body, int ExpectedVersion);
public sealed record MessageSentRequest(int ExpectedVersion, string Receipt);
public sealed record RecordReplyRequest(Guid? ContactId, MessageChannel Channel, string From, string? Subject, string Body, ReplyIntent Intent, DateTime? ReceivedAt);
public sealed record ClassifyReplyRequest(ReplyIntent Intent, int ExpectedVersion);

public sealed record PlanMeetingRequest(
    string Title, DateTime StartsAt, string TimeZone, int DurationMinutes, string Invitees, string? Agenda, string? Link, int? ExpectedVersion);
public sealed record MeetingInvitedRequest(int ExpectedVersion, string Receipt);
public sealed record FinishMeetingRequest(bool Held, string? Outcome, int ExpectedVersion);
