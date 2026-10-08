using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Staffing;

public enum MessageDirection { Outbound, Inbound }
public enum MessageChannel { Email, LinkedInConnection, LinkedInMessage, InMail, ContactForm, Upwork, Phone, Other }
public enum MessageState { Draft, Approved, Sent, Received }
/// <summary>The user's reading of an inbound message. Never inferred by the server.</summary>
public enum ReplyIntent { Unclassified, Interested, Question, NotNow, NotInterested, Referral, Unsubscribe }

/// <summary>
/// One message in a deal's conversation. Outbound messages are drafted, approved for the exact recipient and text, and
/// then sent by the user (LinkedIn and portals have no permitted send API) who records the receipt. Inbound messages are
/// recorded as received, with the user's own intent label. Nothing here claims a send without that confirmation.
/// </summary>
public sealed class StaffingMessage : IOwned
{
    public const int MaxRecipientLength = 320;
    public const int MaxSubjectLength = 300;
    public const int MaxBodyLength = 10_000;
    public const int MaxReceiptLength = 1_000;

    private StaffingMessage() { }

    private StaffingMessage(Guid ownerId, Guid dealId, Guid? contactId, MessageDirection direction, MessageChannel channel, DateTime utcNow)
    {
        if (ownerId == Guid.Empty || dealId == Guid.Empty) throw new ArgumentException("Owner and deal are required.");
        if (!Enum.IsDefined(channel)) throw new ArgumentOutOfRangeException(nameof(channel));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        DealId = dealId;
        ContactId = contactId;
        Direction = direction;
        Channel = channel;
        Intent = ReplyIntent.Unclassified;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public static StaffingMessage Draft(Guid ownerId, Guid dealId, Guid? contactId, MessageChannel channel, string recipient, string? subject,
        string body, DateTime utcNow)
    {
        var m = new StaffingMessage(ownerId, dealId, contactId, MessageDirection.Outbound, channel, utcNow) { State = MessageState.Draft };
        m.Edit(recipient, subject, body, utcNow);
        return m;
    }

    public static StaffingMessage Received(Guid ownerId, Guid dealId, Guid? contactId, MessageChannel channel, string from, string? subject,
        string body, ReplyIntent intent, DateTime receivedAt, DateTime utcNow)
    {
        if (!Enum.IsDefined(intent)) throw new ArgumentOutOfRangeException(nameof(intent));
        return new StaffingMessage(ownerId, dealId, contactId, MessageDirection.Inbound, channel, utcNow)
        {
            State = MessageState.Received,
            Counterpart = Guard.Required(from, MaxRecipientLength, nameof(from)),
            Subject = Guard.Optional(subject, MaxSubjectLength, nameof(subject)),
            Body = Guard.Required(body, MaxBodyLength, nameof(body)),
            Intent = intent,
            OccurredAt = receivedAt,
            Version = 1,
        };
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid DealId { get; private set; }
    public Guid? ContactId { get; private set; }
    public MessageDirection Direction { get; private set; }
    public MessageChannel Channel { get; private set; }
    /// <summary>Recipient (outbound) or sender (inbound): an email address, profile URL or name.</summary>
    public string Counterpart { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public MessageState State { get; private set; }
    public int? ApprovedVersion { get; private set; }
    public string? Receipt { get; private set; }
    /// <summary>When it was sent (outbound) or received (inbound).</summary>
    public DateTime? OccurredAt { get; private set; }
    public ReplyIntent Intent { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Any edit returns an approved draft to Draft, so approval always matches the exact text and recipient.</summary>
    public void Edit(string recipient, string? subject, string body, DateTime utcNow)
    {
        if (Direction != MessageDirection.Outbound || State is MessageState.Sent) throw new InvalidOperationException("Only an unsent outbound draft can be edited.");
        Counterpart = Guard.Required(recipient, MaxRecipientLength, nameof(recipient));
        Subject = Guard.Optional(subject, MaxSubjectLength, nameof(subject));
        Body = Guard.Required(body, MaxBodyLength, nameof(body));
        if (Channel == MessageChannel.LinkedInConnection && Body.Length > 300)
            throw new ArgumentException("A LinkedIn connection note is at most 300 characters.", nameof(body));
        State = MessageState.Draft;
        ApprovedVersion = null;
        Version++;
        UpdatedAt = utcNow;
    }

    public void Approve(int expectedVersion, DateTime utcNow)
    {
        if (State != MessageState.Draft) throw new InvalidOperationException("Only a draft can be approved.");
        if (expectedVersion != Version) throw new InvalidOperationException($"The draft changed (now version {Version}). Review it again.");
        if (Body.Contains('[') && Body.Contains(']')) throw new InvalidOperationException("Replace every [placeholder] before approving.");
        State = MessageState.Approved;
        Version++;
        ApprovedVersion = Version;
        UpdatedAt = utcNow;
    }

    public void MarkSent(int expectedVersion, string receipt, DateTime utcNow)
    {
        if (State != MessageState.Approved || expectedVersion != Version || ApprovedVersion != Version)
            throw new InvalidOperationException("Only the approved current version can be marked as sent.");
        Receipt = Guard.Required(receipt, MaxReceiptLength, nameof(receipt));
        State = MessageState.Sent;
        OccurredAt = utcNow;
        Version++;
        UpdatedAt = utcNow;
    }

    public void Classify(ReplyIntent intent, DateTime utcNow)
    {
        if (Direction != MessageDirection.Inbound) throw new InvalidOperationException("Only received messages have an intent.");
        if (!Enum.IsDefined(intent)) throw new ArgumentOutOfRangeException(nameof(intent));
        Intent = intent;
        Version++;
        UpdatedAt = utcNow;
    }
}
