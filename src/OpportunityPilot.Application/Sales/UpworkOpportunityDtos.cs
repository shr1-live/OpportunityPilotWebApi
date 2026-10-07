using OpportunityPilot.Domain.Sales;

namespace OpportunityPilot.Application.Sales;

public sealed record UpworkOpportunityDto(
    Guid Id, string ProviderJobId, string Title, string Url, string? Summary, string? Location,
    UpworkBudgetType BudgetType, decimal? BudgetMin, decimal? BudgetMax, string? Currency,
    string? ExperienceLevel, int? ConnectsRequired, int? AvailableConnectsAtReview,
    string ConnectsStatus, bool? PaymentVerified, DateTime? PostedAt, DateTime ObservedAt,
    string EvidenceJson, UpworkOpportunityState State, Guid? SalesProjectId, int Version,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record ImportUpworkOpportunityRequest(
    string ProviderJobId, string Title, string Url, string? Summary, string? Location,
    UpworkBudgetType BudgetType, decimal? BudgetMin, decimal? BudgetMax, string? Currency,
    string? ExperienceLevel, int? ConnectsRequired, int? AvailableConnectsAtReview,
    bool? PaymentVerified, DateTime? PostedAt, DateTime? ObservedAt, string? EvidenceJson);

public sealed record DecideUpworkOpportunityRequest(UpworkOpportunityState State, int ExpectedVersion);

