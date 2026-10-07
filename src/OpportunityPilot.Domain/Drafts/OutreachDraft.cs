using System.Security.Cryptography;
using System.Text;
using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Drafts;

public enum DraftChannel
{
    Email,
    CoverNote,
    LinkedInMessage,
    ContactForm
}

public enum DraftState
{
    Draft,
    Approved
}

public enum DraftSource
{
    Gemini,
    Template
}

/// <summary>A user-owned, versioned message proposal. Approval is valid only for the exact saved version and content.</summary>
public sealed class OutreachDraft : IOwned
{
    public const int MaxRecipientLength = 320;
    public const int MaxSubjectLength = 300;
    public const int MaxBodyLength = 10_000;
    public const int HashLength = 64;

    private OutreachDraft() { }

    public OutreachDraft(
        Guid ownerId, Guid opportunityId, DraftChannel channel, string? recipient, bool recipientVerified,
        string? subject, string body, DraftSource source, string claimsJson, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (opportunityId == Guid.Empty) throw new ArgumentException("Opportunity is required.", nameof(opportunityId));
        if (!Enum.IsDefined(channel)) throw new ArgumentOutOfRangeException(nameof(channel));
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        OpportunityId = opportunityId;
        Channel = channel;
        Recipient = Guard.Optional(recipient, MaxRecipientLength, nameof(recipient));
        RecipientVerified = recipientVerified;
        Subject = Guard.Optional(subject, MaxSubjectLength, nameof(subject));
        Body = RequiredBody(body);
        Source = source;
        ClaimsJson = string.IsNullOrWhiteSpace(claimsJson) ? "[]" : claimsJson;
        State = DraftState.Draft;
        Version = 1;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid OpportunityId { get; private set; }
    public DraftChannel Channel { get; private set; }
    public string? Recipient { get; private set; }
    public bool RecipientVerified { get; private set; }
    public string? Subject { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public DraftState State { get; private set; }
    public string? ApprovedHash { get; private set; }
    public int? ApprovedVersion { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DraftSource Source { get; private set; }
    public string ClaimsJson { get; private set; } = "[]";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string? recipient, bool recipientVerified, string? subject, string body, DateTime utcNow)
    {
        var nextRecipient = Guard.Optional(recipient, MaxRecipientLength, nameof(recipient));
        var nextSubject = Guard.Optional(subject, MaxSubjectLength, nameof(subject));
        var nextBody = RequiredBody(body);
        if (Recipient == nextRecipient && RecipientVerified == recipientVerified && Subject == nextSubject && Body == nextBody) return;

        Recipient = nextRecipient;
        RecipientVerified = recipientVerified;
        Subject = nextSubject;
        Body = nextBody;
        Version++;
        UpdatedAt = utcNow;
        ClearApproval();
    }

    public void Approve(int version, DateTime utcNow)
    {
        if (version != Version) throw new InvalidOperationException("The draft version is stale.");
        if (string.IsNullOrWhiteSpace(Body)) throw new InvalidOperationException("The draft body is required.");
        if (Channel == DraftChannel.Email && string.IsNullOrWhiteSpace(Recipient))
            throw new InvalidOperationException("Email drafts require a recipient.");

        State = DraftState.Approved;
        ApprovedVersion = Version;
        ApprovedAt = utcNow;
        ApprovedHash = ContentHash();
        UpdatedAt = utcNow;
    }

    public void RevokeApproval(DateTime utcNow)
    {
        if (State == DraftState.Draft && ApprovedHash is null) return;
        ClearApproval();
        UpdatedAt = utcNow;
    }

    public bool HasValidApproval()
    {
        if (State != DraftState.Approved || ApprovedVersion != Version || ApprovedHash?.Length != HashLength) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(ApprovedHash), Convert.FromHexString(ContentHash()));
        }
        catch (FormatException) { return false; }
    }

    private string ContentHash()
    {
        var canonical = $"{Id:D}|{Version}|{Channel}|{Recipient ?? string.Empty}|{RecipientVerified}|{Subject ?? string.Empty}|{Body}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string RequiredBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("body is required.", nameof(body));
        if (body.Length > MaxBodyLength) throw new ArgumentException($"body must be at most {MaxBodyLength} characters.", nameof(body));
        return body;
    }

    private void ClearApproval()
    {
        State = DraftState.Draft;
        ApprovedHash = null;
        ApprovedVersion = null;
        ApprovedAt = null;
    }
}
