using OpportunityPilot.Domain.Sales;

namespace OpportunityPilot.Application.Sales;

public sealed record SalesProjectDto(
    Guid Id, SalesProjectSource Source, string? ExternalId, string Title, string? Buyer, string? Description,
    string? Url, DateTime? DeadlineUtc, SalesProjectState State, int Version, IReadOnlyList<SalesBidDto> Bids,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record SalesBidDto(
    Guid Id, Guid ProjectId, decimal Amount, string Currency, int DeliveryDays, string Proposal, int Version,
    SalesBidState State, int? ApprovedVersion, DateTime? ApprovedAt, bool HasValidApproval, DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateSalesProjectRequest(
    SalesProjectSource Source, string? ExternalId, string Title, string? Buyer, string? Description, string? Url,
    DateTime? DeadlineUtc, string? EvidenceJson);

public sealed record CreateSalesBidRequest(
    decimal Amount, string Currency, int DeliveryDays, string Proposal);

public sealed record UpdateSalesBidRequest(
    decimal Amount, string Currency, int DeliveryDays, string Proposal, int ExpectedVersion);

public sealed record ApproveSalesBidRequest(int Version);
public sealed record BatchApproveSalesBidItem(Guid Id, int Version);
public sealed record BatchApproveSalesBidsRequest(IReadOnlyList<BatchApproveSalesBidItem> Items);
public sealed record BatchApproveSalesBidResult(Guid Id, bool Approved, string? Reason, SalesProjectDto? Project);
