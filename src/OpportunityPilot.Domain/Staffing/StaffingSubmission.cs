using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Staffing;

public enum SubmissionState { Draft, Approved, Sent, Withdrawn }
public enum HandoffChannel { Email, ClientPortal, Manual }

/// <summary>
/// What is shared about one candidate with one client deal. The snapshot (the exact values shared) is frozen at approval;
/// any change sends it back to Draft. Sending is a recorded handoff with a receipt — nothing is delivered silently.
/// </summary>
public sealed class StaffingSubmission : IOwned
{
    public const int MaxSnapshotLength = 40_000;
    public const int MaxNoteLength = 2_000;
    public const int MaxReceiptLength = 1_000;

    private StaffingSubmission() { }

    public StaffingSubmission(Guid ownerId, Guid dealId, Guid candidateId, CandidateField fields, string snapshotJson,
        int candidateVersion, int resumeVersion, string? note, DateTime utcNow)
    {
        if (ownerId == Guid.Empty || dealId == Guid.Empty || candidateId == Guid.Empty)
            throw new ArgumentException("Owner, deal and candidate are required.");
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        DealId = dealId;
        CandidateId = candidateId;
        State = SubmissionState.Draft;
        CreatedAt = utcNow;
        Revise(fields, snapshotJson, candidateVersion, resumeVersion, note, utcNow);
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid DealId { get; private set; }
    public Guid CandidateId { get; private set; }
    public CandidateField SharedFields { get; private set; }
    public string SnapshotJson { get; private set; } = "{}";
    public int CandidateVersion { get; private set; }
    public int ResumeVersion { get; private set; }
    public string? Note { get; private set; }
    public SubmissionState State { get; private set; }
    public int? ApprovedVersion { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public HandoffChannel? Channel { get; private set; }
    public string? Receipt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>A new snapshot or note: back to Draft, so approval always matches what is shared.</summary>
    public void Revise(CandidateField fields, string snapshotJson, int candidateVersion, int resumeVersion, string? note, DateTime utcNow)
    {
        if (State is SubmissionState.Sent or SubmissionState.Withdrawn)
            throw new InvalidOperationException($"A {State.ToString().ToLowerInvariant()} submission cannot be changed.");
        if (fields == CandidateField.None) throw new ArgumentException("Choose at least one field to share.", nameof(fields));
        SharedFields = fields;
        SnapshotJson = Guard.Required(snapshotJson, MaxSnapshotLength, nameof(snapshotJson));
        CandidateVersion = candidateVersion;
        ResumeVersion = resumeVersion;
        Note = Guard.Optional(note, MaxNoteLength, nameof(note));
        State = SubmissionState.Draft;
        ApprovedVersion = null;
        ApprovedAt = null;
        Version++;
        UpdatedAt = utcNow;
    }

    public void Approve(int expectedVersion, DateTime utcNow)
    {
        if (State != SubmissionState.Draft) throw new InvalidOperationException("Only a draft submission can be approved.");
        if (expectedVersion != Version) throw new InvalidOperationException($"The submission changed (now version {Version}). Review it again.");
        State = SubmissionState.Approved;
        Version++;
        ApprovedVersion = Version;
        ApprovedAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>The user confirms they handed over exactly the approved version, with a receipt (message id, portal ref…).</summary>
    public void MarkSent(int expectedVersion, HandoffChannel channel, string receipt, DateTime utcNow)
    {
        if (State != SubmissionState.Approved) throw new InvalidOperationException("Only an approved submission can be marked as sent.");
        if (expectedVersion != Version || ApprovedVersion != Version) throw new InvalidOperationException("Approve the current version first.");
        if (!Enum.IsDefined(channel)) throw new ArgumentOutOfRangeException(nameof(channel));
        Receipt = Guard.Required(receipt, MaxReceiptLength, nameof(receipt));
        Channel = channel;
        State = SubmissionState.Sent;
        SentAt = utcNow;
        Version++;
        UpdatedAt = utcNow;
    }

    public void Withdraw(DateTime utcNow)
    {
        if (State == SubmissionState.Withdrawn) return;
        State = SubmissionState.Withdrawn;
        Version++;
        UpdatedAt = utcNow;
    }
}
