using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Profiles;

/// <summary>Immutable readable snapshot of a profile version used to explain past campaign decisions.</summary>
public sealed class ProfileVersion : IOwned
{
    private ProfileVersion() { }

    public ProfileVersion(Profile profile, DateTime utcNow)
    {
        Id = Guid.NewGuid();
        OwnerId = profile.OwnerId;
        ProfileId = profile.Id;
        Version = profile.Version;
        Type = profile.Type;
        Name = profile.Name;
        StructuredDataJson = profile.StructuredDataJson;
        ConfirmedAt = profile.ConfirmedAt;
        CreatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid ProfileId { get; private set; }
    public int Version { get; private set; }
    public ProfileType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string StructuredDataJson { get; private set; } = "{}";
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
