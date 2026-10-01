using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Campaigns;

/// <summary>
/// What to look for, judged against one of the owner's profiles. Criteria and weights are stored as JSON
/// the application layer validates and normalises; every save increments <see cref="Version"/>.
/// </summary>
public class Campaign : IOwned
{
    public const int MaxNameLength = 200;
    public const int MaxGoalLength = 2000;
    public const int MinResultLimit = 1;
    public const int MaxResultLimit = 100;
    public const int DefaultResultLimit = 25;

    private Campaign() { }

    public Campaign(
        Guid ownerId, Guid profileId, OpportunityMode mode, string name, string? goal,
        string criteriaJson, string weightsJson, int resultLimit, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (profileId == Guid.Empty) throw new ArgumentException("Profile is required.", nameof(profileId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        ProfileId = profileId;
        Mode = mode;
        CreatedAt = utcNow;
        Version = 0;
        Apply(name, goal, criteriaJson, weightsJson, resultLimit, utcNow);
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid ProfileId { get; private set; }

    /// <summary>Fixed at creation: criteria keys and weights only make sense for one mode.</summary>
    public OpportunityMode Mode { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string Goal { get; private set; } = string.Empty;
    public string CriteriaJson { get; private set; } = "{}";
    public string WeightsJson { get; private set; } = "{}";
    public int ResultLimit { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string name, string? goal, string criteriaJson, string weightsJson, int resultLimit, DateTime utcNow) =>
        Apply(name, goal, criteriaJson, weightsJson, resultLimit, utcNow);

    private void Apply(string name, string? goal, string criteriaJson, string weightsJson, int resultLimit, DateTime utcNow)
    {
        if (resultLimit is < MinResultLimit or > MaxResultLimit)
            throw new ArgumentOutOfRangeException(nameof(resultLimit), $"Result limit must be {MinResultLimit}–{MaxResultLimit}.");
        Name = Guard.Required(name, MaxNameLength, nameof(name));
        Goal = Guard.Optional(goal, MaxGoalLength, nameof(goal)) ?? string.Empty;
        CriteriaJson = string.IsNullOrWhiteSpace(criteriaJson) ? "{}" : criteriaJson;
        WeightsJson = string.IsNullOrWhiteSpace(weightsJson) ? "{}" : weightsJson;
        ResultLimit = resultLimit;
        UpdatedAt = utcNow;
        Version++;
    }
}
