namespace OpportunityPilot.Application.Abstractions;

/// <summary>The authenticated caller. OwnerId always comes from the validated token, never from the request body.</summary>
public interface ICurrentUser
{
    Guid OwnerId { get; }
}
