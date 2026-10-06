using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Outreach;

public enum NextActionKind { FollowUp, CheckStatus, Call, Other }
public enum NextActionState { Open, Done, Cancelled }

public sealed class NextAction : IOwned
{
    public const int MaxNoteLength = 500;
    public const int MaxTimeZoneLength = 100;

    private NextAction() { }

    public NextAction(Guid ownerId, Guid opportunityId, NextActionKind kind, string? note, DateTime dueAt, string timeZone, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (opportunityId == Guid.Empty) throw new ArgumentException("Opportunity is required.", nameof(opportunityId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        OpportunityId = opportunityId;
        Kind = kind;
        Note = Guard.Truncate(note, MaxNoteLength);
        DueAt = NormalizeUtc(dueAt);
        TimeZone = Guard.Required(timeZone, MaxTimeZoneLength, nameof(timeZone));
        State = NextActionState.Open;
        CreatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid OpportunityId { get; private set; }
    public NextActionKind Kind { get; private set; }
    public string Note { get; private set; } = string.Empty;
    public DateTime DueAt { get; private set; }
    public string TimeZone { get; private set; } = string.Empty;
    public NextActionState State { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public void Update(NextActionState? state, DateTime? dueAt, DateTime utcNow)
    {
        if (state is { } next && !Enum.IsDefined(next)) throw new ArgumentOutOfRangeException(nameof(state));
        if (dueAt is { } due) DueAt = NormalizeUtc(due);
        if (state is { } value)
        {
            State = value;
            CompletedAt = value == NextActionState.Open ? null : utcNow;
        }
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
