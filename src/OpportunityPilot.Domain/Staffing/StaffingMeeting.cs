using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Staffing;

public enum MeetingState { Proposed, Approved, Invited, Held, Cancelled }

/// <summary>
/// A client discussion. It is approved before anyone is invited; the invite itself is sent by the user (calendar or
/// scheduling link) and recorded with a receipt. Rescheduling an invited meeting needs approval and a new invite.
/// </summary>
public sealed class StaffingMeeting : IOwned
{
    public const int MaxTitleLength = 300;
    public const int MaxInviteesLength = 2_000;
    public const int MaxAgendaLength = 4_000;
    public const int MaxLinkLength = 1_000;

    private StaffingMeeting() { }

    public StaffingMeeting(Guid ownerId, Guid dealId, DateTime utcNow)
    {
        if (ownerId == Guid.Empty || dealId == Guid.Empty) throw new ArgumentException("Owner and deal are required.");
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        DealId = dealId;
        State = MeetingState.Proposed;
        CreatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid DealId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime StartsAt { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public int DurationMinutes { get; private set; }
    /// <summary>Comma-separated names/addresses, as entered.</summary>
    public string Invitees { get; private set; } = string.Empty;
    public string? Agenda { get; private set; }
    public string? Link { get; private set; }
    public MeetingState State { get; private set; }
    public string? Receipt { get; private set; }
    public string? Outcome { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Sets or changes the plan. Any change after approval or invitation needs approval (and a new invite) again.</summary>
    public void Plan(string title, DateTime startsAtUtc, string timeZone, int durationMinutes, string invitees, string? agenda, string? link, DateTime utcNow)
    {
        if (State is MeetingState.Held or MeetingState.Cancelled) throw new InvalidOperationException("A finished meeting cannot be changed.");
        if (durationMinutes is < 5 or > 600) throw new ArgumentException("Duration must be 5–600 minutes.", nameof(durationMinutes));
        Title = Guard.Required(title, MaxTitleLength, nameof(title));
        StartsAt = DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc);
        TimeZone = Guard.Required(timeZone, 64, nameof(timeZone));
        DurationMinutes = durationMinutes;
        Invitees = Guard.Required(invitees, MaxInviteesLength, nameof(invitees));
        Agenda = Guard.Optional(agenda, MaxAgendaLength, nameof(agenda));
        Link = Guard.Optional(link, MaxLinkLength, nameof(link));
        State = MeetingState.Proposed;
        Receipt = null;
        Version++;
        UpdatedAt = utcNow;
    }

    public void Approve(int expectedVersion, DateTime utcNow)
    {
        if (State != MeetingState.Proposed) throw new InvalidOperationException("Only a proposed meeting can be approved.");
        if (expectedVersion != Version) throw new InvalidOperationException($"The meeting changed (now version {Version}). Review it again.");
        State = MeetingState.Approved;
        Version++;
        UpdatedAt = utcNow;
    }

    public void MarkInvited(int expectedVersion, string receipt, DateTime utcNow)
    {
        if (State != MeetingState.Approved || expectedVersion != Version) throw new InvalidOperationException("Approve the current plan before recording the invite.");
        Receipt = Guard.Required(receipt, 1_000, nameof(receipt));
        State = MeetingState.Invited;
        Version++;
        UpdatedAt = utcNow;
    }

    public void Finish(bool held, string? outcome, DateTime utcNow)
    {
        if (held && State != MeetingState.Invited) throw new InvalidOperationException("Only a meeting people were invited to can be marked as held.");
        if (State is MeetingState.Held or MeetingState.Cancelled) throw new InvalidOperationException("The meeting is already finished.");
        State = held ? MeetingState.Held : MeetingState.Cancelled;
        Outcome = Guard.Optional(outcome, MaxAgendaLength, nameof(outcome));
        Version++;
        UpdatedAt = utcNow;
    }
}
