using System.Security.Cryptography;
using System.Text;
using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Research;

/// <summary>
/// A bounded excerpt of what a source actually said, kept so every scored claim can point at its origin.
/// Immutable: a rerun that sees the same excerpt from the same source reuses the row (same <see cref="ContentHash"/>).
/// </summary>
public class Evidence : IOwned
{
    public const int MaxExcerptLength = 8000;
    public const int MaxUrlLength = 1000;
    public const string RulesMethod = "Rules";

    private Evidence() { }

    public Evidence(Guid ownerId, Guid campaignId, Guid sourceId, string? url, DateTime retrievedAt, string excerpt, string extractionMethod)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign is required.", nameof(campaignId));
        if (sourceId == Guid.Empty) throw new ArgumentException("Source is required.", nameof(sourceId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CampaignId = campaignId;
        SourceId = sourceId;
        Url = Guard.TruncateOptional(url, MaxUrlLength);
        RetrievedAt = retrievedAt;
        Excerpt = Bound(excerpt);
        ContentHash = HashOf(excerpt);
        ExtractionMethod = Guard.Required(extractionMethod, 32, nameof(extractionMethod));
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid SourceId { get; private set; }
    public string? Url { get; private set; }
    public DateTime RetrievedAt { get; private set; }

    /// <summary>Lowercase hex SHA-256 of <see cref="Excerpt"/>.</summary>
    public string ContentHash { get; private set; } = string.Empty;

    public string Excerpt { get; private set; } = string.Empty;
    public string ExtractionMethod { get; private set; } = RulesMethod;

    /// <summary>Hash of the excerpt exactly as it would be stored (trimmed and bounded).</summary>
    public static string HashOf(string excerpt) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Bound(excerpt))));

    private static string Bound(string excerpt) => Guard.Truncate(excerpt, MaxExcerptLength);
}
