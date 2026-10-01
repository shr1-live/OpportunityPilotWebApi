using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Research;

/// <summary>A safe, user-facing progress line. Never holds stack traces, keys or raw page content.</summary>
public class ResearchEvent
{
    public const int MaxMessageLength = 500;

    private ResearchEvent() { }

    public ResearchEvent(Guid jobId, DateTime at, ResearchStage stage, EventLevel level, string message)
    {
        if (jobId == Guid.Empty) throw new ArgumentException("Job is required.", nameof(jobId));
        Id = Guid.NewGuid();
        JobId = jobId;
        At = at;
        Stage = stage;
        Level = level;
        Message = Guard.Truncate(message, MaxMessageLength);
    }

    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public DateTime At { get; private set; }
    public ResearchStage Stage { get; private set; }
    public EventLevel Level { get; private set; }
    public string Message { get; private set; } = string.Empty;
}
