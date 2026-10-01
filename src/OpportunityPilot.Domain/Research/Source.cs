using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Opportunities;

namespace OpportunityPilot.Domain.Research;

/// <summary>
/// Where a campaign's candidates come from. Paste keeps its text here; Csv and Agent keep rows in
/// <see cref="SourceItem"/>; Url and Feed are fetched (safely) when research runs.
/// </summary>
public class Source : IOwned
{
    public const int MaxLabelLength = 200;
    public const int MaxUrlLength = 1000;
    public const int MaxTextLength = 50_000;
    public const int MaxPermissionNoteLength = 500;
    public const int MaxSafeErrorLength = 500;

    private Source() { }

    public Source(
        Guid ownerId, Guid campaignId, SourceKind kind, string label, string? url, string? text,
        string? permissionNote, JobPlatform? platform, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign is required.", nameof(campaignId));
        switch (kind)
        {
            case SourceKind.Paste when string.IsNullOrWhiteSpace(text):
                throw new ArgumentException("Pasted text is required.", nameof(text));
            case SourceKind.Url or SourceKind.Feed when string.IsNullOrWhiteSpace(url):
                throw new ArgumentException("URL is required.", nameof(url));
            case SourceKind.Agent when platform is null:
                throw new ArgumentException("Agent sources need a platform.", nameof(platform));
        }
        if (text is { Length: > MaxTextLength }) throw new ArgumentException($"Text must be at most {MaxTextLength} characters.", nameof(text));

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CampaignId = campaignId;
        Kind = kind;
        Label = Guard.Required(label, MaxLabelLength, nameof(label));
        Url = Guard.Optional(url, MaxUrlLength, nameof(url));
        Text = kind == SourceKind.Paste ? text : null;
        PermissionNote = Guard.Optional(permissionNote, MaxPermissionNoteLength, nameof(permissionNote));
        Platform = platform;
        Status = SourceStatus.Pending;
        CreatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid CampaignId { get; private set; }
    public SourceKind Kind { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string? Url { get; private set; }
    public string? Text { get; private set; }
    public string? PermissionNote { get; private set; }
    public JobPlatform? Platform { get; private set; }
    public SourceStatus Status { get; private set; }
    public DateTime? LastFetchedAt { get; private set; }
    public string? SafeError { get; private set; }

    /// <summary>Rows held (Csv, Agent), postings parsed (Paste) or entries found at the last fetch (Url, Feed).</summary>
    public int ItemCount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public void SetItemCount(int count) => ItemCount = Math.Max(0, count);

    /// <summary>Records what the last research run got from this source. A failure keeps only a safe, user-facing reason.</summary>
    public void RecordGather(SourceStatus status, string? safeError, int itemCount, DateTime utcNow)
    {
        if (status == SourceStatus.Pending) throw new ArgumentException("A gathered source cannot be pending.", nameof(status));
        Status = status;
        SafeError = status == SourceStatus.Ok ? null : Guard.TruncateOptional(safeError, MaxSafeErrorLength);
        ItemCount = Math.Max(0, itemCount);
        LastFetchedAt = utcNow;
    }

    /// <summary>The desktop agent delivered fresh postings; the source waits for the next research run.</summary>
    public void MarkDelivered(int itemCount, DateTime utcNow)
    {
        ItemCount = Math.Max(0, itemCount);
        LastFetchedAt = utcNow;
        Status = SourceStatus.Pending;
        SafeError = null;
    }
}
