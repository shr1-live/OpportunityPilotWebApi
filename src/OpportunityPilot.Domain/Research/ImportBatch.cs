using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Research;

/// <summary>A previewed CSV waiting for the user to commit it. Expires after an hour and can be committed once.</summary>
public class ImportBatch : IOwned
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private ImportBatch() { }

    public ImportBatch(Guid ownerId, Guid campaignId, string rowsJson, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign is required.", nameof(campaignId));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CampaignId = campaignId;
        RowsJson = string.IsNullOrWhiteSpace(rowsJson) ? "[]" : rowsJson;
        CreatedAt = utcNow;
        ExpiresAt = utcNow + Lifetime;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid CampaignId { get; private set; }
    public string RowsJson { get; private set; } = "[]";
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool Committed { get; private set; }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    public void Commit(DateTime utcNow)
    {
        if (Committed) throw new InvalidOperationException("This import was already committed.");
        if (IsExpired(utcNow)) throw new InvalidOperationException("This import preview has expired.");
        Committed = true;
    }
}
