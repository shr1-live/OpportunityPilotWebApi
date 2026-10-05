namespace OpportunityPilot.Domain.Opportunities;

/// <summary>Stored as strings, so the member order here is for reading only.</summary>
public enum OpportunityStatus
{
    New,

    /// <summary>Research proposed it for the batch approval queue (campaign AutoSuggestMinScore); the user approves or rejects.</summary>
    Suggested,

    Shortlisted,
    Dismissed,
    Applied,
    Contacted,
    Responded,
    Interested,
    Closed
}
