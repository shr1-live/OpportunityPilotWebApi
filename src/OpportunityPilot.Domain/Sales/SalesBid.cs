using System.Security.Cryptography;
using System.Text;
using OpportunityPilot.Domain.Common;

namespace OpportunityPilot.Domain.Sales;

public enum SalesBidState
{
    Draft,
    Approved,
    Placed,
    Failed
}

/// <summary>A versioned bid. Approval is bound to the exact amount, timing and proposal text.</summary>
public sealed class SalesBid : IOwned
{
    public const int MaxCurrencyLength = 3;
    public const int MaxProposalLength = 10_000;
    public const int HashLength = 64;

    private SalesBid() { }

    public SalesBid(Guid ownerId, Guid projectId, decimal amount, string currency, int deliveryDays, string proposal, DateTime utcNow)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (projectId == Guid.Empty) throw new ArgumentException("Project is required.", nameof(projectId));
        ValidateAmount(amount);

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        ProjectId = projectId;
        Amount = amount;
        Currency = RequiredCurrency(currency);
        DeliveryDays = RequiredDeliveryDays(deliveryDays);
        Proposal = RequiredProposal(proposal);
        ClaimsJson = "[]";
        State = SalesBidState.Draft;
        Version = 1;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid ProjectId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public int DeliveryDays { get; private set; }
    public string Proposal { get; private set; } = string.Empty;
    public string ClaimsJson { get; private set; } = "[]";
    public int Version { get; private set; }
    public SalesBidState State { get; private set; }
    public string? ApprovedHash { get; private set; }
    public int? ApprovedVersion { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(int expectedVersion, decimal amount, string currency, int deliveryDays, string proposal, DateTime utcNow)
    {
        if (expectedVersion != Version) throw new InvalidOperationException("The bid version is stale.");
        ValidateAmount(amount);
        var nextCurrency = RequiredCurrency(currency);
        var nextDays = RequiredDeliveryDays(deliveryDays);
        var nextProposal = RequiredProposal(proposal);
        if (Amount == amount && Currency == nextCurrency && DeliveryDays == nextDays && Proposal == nextProposal) return;

        Amount = amount;
        Currency = nextCurrency;
        DeliveryDays = nextDays;
        Proposal = nextProposal;
        Version++;
        State = SalesBidState.Draft;
        ApprovedHash = null;
        ApprovedVersion = null;
        ApprovedAt = null;
        UpdatedAt = utcNow;
    }

    public void Approve(int version, string providerContext, DateTime utcNow)
    {
        if (version != Version) throw new InvalidOperationException("The bid version is stale.");
        State = SalesBidState.Approved;
        ApprovedVersion = Version;
        ApprovedAt = utcNow;
        ApprovedHash = ContentHash(providerContext);
        UpdatedAt = utcNow;
    }

    public void Approve(int version, DateTime utcNow) => Approve(version, string.Empty, utcNow);

    public void MarkPlaced(string providerContext, DateTime utcNow)
    {
        if (State == SalesBidState.Placed) return;
        if (!HasValidApproval(providerContext)) throw new InvalidOperationException("Only the approved bid version can be placed.");
        State = SalesBidState.Placed;
        UpdatedAt = utcNow;
    }

    public void MarkPlaced(DateTime utcNow) => MarkPlaced(string.Empty, utcNow);

    public bool HasValidApproval(string providerContext)
    {
        if (State != SalesBidState.Approved || ApprovedVersion != Version || ApprovedHash?.Length != HashLength) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(ApprovedHash), Convert.FromHexString(ContentHash(providerContext)));
        }
        catch (FormatException) { return false; }
    }

    public bool HasValidApproval() => HasValidApproval(string.Empty);

    private string ContentHash(string providerContext)
    {
        var canonical = $"{Id:D}|{Version}|{Amount:0.############################}|{Currency}|{DeliveryDays}|{Proposal}|{providerContext}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || amount > 1_000_000_000m) throw new ArgumentOutOfRangeException(nameof(amount));
    }

    private static string RequiredCurrency(string currency)
    {
        var value = Guard.Required(currency, MaxCurrencyLength, nameof(currency)).ToUpperInvariant();
        if (value.Any(ch => ch is < 'A' or > 'Z')) throw new ArgumentException("currency must be an ISO alphabetic code.", nameof(currency));
        return value;
    }

    private static int RequiredDeliveryDays(int deliveryDays)
    {
        if (deliveryDays is < 1 or > 3_650) throw new ArgumentOutOfRangeException(nameof(deliveryDays));
        return deliveryDays;
    }

    private static string RequiredProposal(string proposal) => Guard.Required(proposal, MaxProposalLength, nameof(proposal));
}
