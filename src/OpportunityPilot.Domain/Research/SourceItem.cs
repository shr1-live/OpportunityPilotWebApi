using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Research;

/// <summary>
/// One row of a Csv or Agent source: a job posting (Title + Organization) or a company (Title = Organization = name).
/// Agent rows are keyed by the platform's job id so repeated deliveries update instead of duplicating.
/// </summary>
public class SourceItem : IOwned
{
    public const int MaxExternalIdLength = 100;
    public const int MaxTitleLength = 300;
    public const int MaxOrganizationLength = 300;
    public const int MaxLocationLength = 200;
    public const int MaxUrlLength = 1000;
    public const int MaxDescriptionLength = 20_000;
    public const int MaxCountryLength = 100;
    public const int MaxIndustryLength = 200;

    private SourceItem() { }

    public SourceItem(
        Guid ownerId, Guid sourceId, string? externalId, string title, string? organization, string? location,
        string? url, string? description, string? website, string? country, string? industry, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (sourceId == Guid.Empty) throw new ArgumentException("Source is required.", nameof(sourceId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        SourceId = sourceId;
        ExternalId = Guard.Optional(externalId, MaxExternalIdLength, nameof(externalId));
        CreatedAt = utcNow;
        Update(title, organization, location, url, description, website, country, industry, utcNow);
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid SourceId { get; private set; }
    public string? ExternalId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Organization { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public string? Url { get; private set; }
    public string? Description { get; private set; }
    public string? Website { get; private set; }
    public string? Country { get; private set; }
    public string? Industry { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(
        string title, string? organization, string? location, string? url, string? description,
        string? website, string? country, string? industry, DateTime utcNow)
    {
        Title = Guard.Required(title, MaxTitleLength, nameof(title));
        Organization = Guard.Optional(organization, MaxOrganizationLength, nameof(organization)) ?? string.Empty;
        Location = Guard.Optional(location, MaxLocationLength, nameof(location));
        Url = Guard.Optional(url, MaxUrlLength, nameof(url));
        Description = Guard.Optional(description, MaxDescriptionLength, nameof(description));
        Website = Guard.Optional(website, MaxUrlLength, nameof(website));
        Country = Guard.Optional(country, MaxCountryLength, nameof(country));
        Industry = Guard.Optional(industry, MaxIndustryLength, nameof(industry));
        UpdatedAt = utcNow;
    }
}
