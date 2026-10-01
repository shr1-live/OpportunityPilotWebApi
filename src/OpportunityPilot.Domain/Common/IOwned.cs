namespace OpportunityPilot.Domain.Common;

/// <summary>Every user-owned record carries the owner identity derived from the validated token.</summary>
public interface IOwned
{
    Guid OwnerId { get; }
}
