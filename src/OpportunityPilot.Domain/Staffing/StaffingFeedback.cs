using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Staffing;

public enum FeedbackSource { Client, Candidate, Internal }
public enum FeedbackDecision { None, NextRound, Selected, Rejected, OnHold }

/// <summary>
/// Feedback on a submission or interview. Internal notes can never be shared with the candidate; client or candidate
/// feedback reaches the candidate only when it is explicitly marked as shared.
/// </summary>
public sealed class StaffingFeedback : IOwned
{
    public const int MaxDetailLength = 4_000;

    private StaffingFeedback() { }

    public StaffingFeedback(Guid ownerId, Guid dealId, Guid submissionId, Guid? interviewId, FeedbackSource source,
        FeedbackDecision decision, string detail, bool sharedWithCandidate, DateTime utcNow)
    {
        if (ownerId == Guid.Empty || dealId == Guid.Empty || submissionId == Guid.Empty)
            throw new ArgumentException("Owner, deal and submission are required.");
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        if (!Enum.IsDefined(decision)) throw new ArgumentOutOfRangeException(nameof(decision));
        if (source == FeedbackSource.Internal && sharedWithCandidate)
            throw new ArgumentException("Internal notes are never shared with the candidate.", nameof(sharedWithCandidate));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        DealId = dealId;
        SubmissionId = submissionId;
        InterviewId = interviewId;
        Source = source;
        Decision = decision;
        Detail = Guard.Required(detail, MaxDetailLength, nameof(detail));
        SharedWithCandidate = sharedWithCandidate;
        RecordedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid DealId { get; private set; }
    public Guid SubmissionId { get; private set; }
    public Guid? InterviewId { get; private set; }
    public FeedbackSource Source { get; private set; }
    public FeedbackDecision Decision { get; private set; }
    public string Detail { get; private set; } = string.Empty;
    public bool SharedWithCandidate { get; private set; }
    public DateTime RecordedAt { get; private set; }
}
