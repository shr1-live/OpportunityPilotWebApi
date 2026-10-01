using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Profiles;

/// <summary>
/// A product, business, candidate or services description a campaign judges fit against.
/// Structured fields live in <see cref="StructuredDataJson"/>; each save increments <see cref="Version"/>.
/// </summary>
public class Profile : IOwned
{
    private Profile() { }

    public Profile(Guid ownerId, ProfileType type, string name, string structuredDataJson, bool confirmed, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Type = type;
        CreatedAt = utcNow;
        Version = 0;
        Apply(name, structuredDataJson, confirmed, utcNow);
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public ProfileType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string StructuredDataJson { get; private set; } = "{}";
    public int Version { get; private set; }

    /// <summary>Set only when the user has confirmed every confirmable claim in the current version.</summary>
    public DateTime? ConfirmedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string name, string structuredDataJson, bool confirmed, DateTime utcNow) =>
        Apply(name, structuredDataJson, confirmed, utcNow);

    private void Apply(string name, string structuredDataJson, bool confirmed, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
        StructuredDataJson = string.IsNullOrWhiteSpace(structuredDataJson) ? "{}" : structuredDataJson;
        ConfirmedAt = confirmed ? utcNow : null;
        UpdatedAt = utcNow;
        Version++;
    }
}
