namespace OpportunityPilot.Domain.Research;

public enum ResearchJobState
{
    Queued,
    Running,
    Completed,
    CompletedWithGaps,
    Failed,
    Cancelled
}
