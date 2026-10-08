using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Staffing;

public enum OfferState { Draft, Extended, Accepted, Declined, Withdrawn }
public enum ContractStatus { NotStarted, Sent, Signed, Cancelled }
public enum PlacementOutcome { Pending, Placed, FellThrough }

/// <summary>
/// The commercial offer to the client and the candidate offer for one submission, through to contract and placement.
/// Signed documents are referenced (e.g. an e-signature envelope id or a private storage key), never stored or exposed.
/// </summary>
public sealed class StaffingOffer : IOwned
{
    public const int MaxReferenceLength = 500;
    public const int MaxContractVersionLength = 64;
    public const int MaxNotesLength = 2_000;

    private StaffingOffer() { }

    public StaffingOffer(Guid ownerId, Guid dealId, Guid submissionId, DateTime utcNow)
    {
        if (ownerId == Guid.Empty || dealId == Guid.Empty || submissionId == Guid.Empty)
            throw new ArgumentException("Owner, deal and submission are required.");
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        DealId = dealId;
        SubmissionId = submissionId;
        State = OfferState.Draft;
        Contract = ContractStatus.NotStarted;
        Outcome = PlacementOutcome.Pending;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid DealId { get; private set; }
    public Guid SubmissionId { get; private set; }
    public decimal? ClientRate { get; private set; }
    public decimal? CandidatePay { get; private set; }
    public string? Currency { get; private set; }
    public RateUnit? Unit { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public decimal? PlacementValue { get; private set; }
    public OfferState State { get; private set; }
    public ContractStatus Contract { get; private set; }
    public string? ContractVersion { get; private set; }
    public string? SignatureProvider { get; private set; }
    public string? SignedDocumentReference { get; private set; }
    public PlacementOutcome Outcome { get; private set; }
    public string? Notes { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void SetTerms(decimal? clientRate, decimal? candidatePay, string? currency, RateUnit? unit, DateOnly? startDate,
        decimal? placementValue, string? notes, DateTime utcNow)
    {
        if (State is OfferState.Accepted or OfferState.Declined or OfferState.Withdrawn)
            throw new InvalidOperationException("Terms are fixed once the offer is accepted, declined or withdrawn.");
        if (clientRate is < 0 || candidatePay is < 0 || placementValue is < 0) throw new ArgumentException("Amounts cannot be negative.");
        if ((clientRate is not null || candidatePay is not null) && (string.IsNullOrWhiteSpace(currency) || unit is null))
            throw new ArgumentException("Rates need a currency and a unit.");
        if (unit is { } u && !Enum.IsDefined(u)) throw new ArgumentOutOfRangeException(nameof(unit));
        ClientRate = clientRate;
        CandidatePay = candidatePay;
        Currency = Guard.Optional(currency?.ToUpperInvariant(), 3, nameof(currency));
        Unit = unit;
        StartDate = startDate;
        PlacementValue = placementValue;
        Notes = Guard.Optional(notes, MaxNotesLength, nameof(notes));
        if (State == OfferState.Extended) State = OfferState.Draft; // changed terms must be extended again
        Version++;
        UpdatedAt = utcNow;
    }

    public void MoveTo(OfferState next, DateTime utcNow)
    {
        var allowed = (State, next) switch
        {
            (OfferState.Draft, OfferState.Extended) => ClientRate is not null || CandidatePay is not null,
            (OfferState.Extended, OfferState.Accepted or OfferState.Declined) => true,
            (OfferState.Draft or OfferState.Extended, OfferState.Withdrawn) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException(State == OfferState.Draft && next == OfferState.Extended
            ? "Set a client rate or candidate pay before extending the offer."
            : $"An offer cannot move from {State} to {next}.");
        State = next;
        Version++;
        UpdatedAt = utcNow;
    }

    public void UpdateContract(ContractStatus status, string? contractVersion, string? signatureProvider, string? signedDocumentReference,
        DateTime utcNow)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (status != ContractStatus.NotStarted && State != OfferState.Accepted)
            throw new InvalidOperationException("Start the contract after the offer is accepted.");
        if (status == ContractStatus.Signed && string.IsNullOrWhiteSpace(signedDocumentReference))
            throw new ArgumentException("A signed contract needs a document reference (e.g. the e-signature envelope id).");
        Contract = status;
        ContractVersion = Guard.Optional(contractVersion, MaxContractVersionLength, nameof(contractVersion));
        SignatureProvider = Guard.Optional(signatureProvider, MaxReferenceLength, nameof(signatureProvider));
        SignedDocumentReference = Guard.Optional(signedDocumentReference, MaxReferenceLength, nameof(signedDocumentReference));
        Version++;
        UpdatedAt = utcNow;
    }

    public void RecordOutcome(PlacementOutcome outcome, DateTime utcNow)
    {
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        if (outcome == PlacementOutcome.Placed && Contract != ContractStatus.Signed)
            throw new InvalidOperationException("A placement needs a signed contract.");
        Outcome = outcome;
        Version++;
        UpdatedAt = utcNow;
    }
}
