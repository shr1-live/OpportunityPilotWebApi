using OpportunityPilot.Domain.Staffing;

namespace OpportunityPilot.Application.Staffing;

public sealed record StaffingContactDto(
    Guid Id, Guid AccountId, string Name, string? Title, string? Email, bool EmailVerified, string? LinkedInUrl,
    string? Evidence, int Version, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record StaffingAccountDto(
    Guid Id, string Name, string? Domain, string? Industry, string? Location, StaffingAccountSource Source,
    string? SourceReference, int Version, IReadOnlyList<StaffingContactDto> Contacts, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record StaffingDealActivityDto(
    Guid Id, StaffingDealActivityType Type, string Detail, DateTime OccurredAt);

public sealed record StaffingDealDto(
    Guid Id, Guid AccountId, Guid? ContactId, string Title, StaffingDealSource Source, string? ExternalReference,
    decimal? EstimatedValue, string? Currency, StaffingDealStage Stage, StaffingDealStage? StageBeforeHold,
    string? NextAction, DateTime? NextActionAt, int Version, IReadOnlyList<StaffingDealActivityDto> Activities,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CreateStaffingAccountRequest(
    string Name, StaffingAccountSource Source, string? Domain, string? Industry, string? Location, string? SourceReference);

public sealed record CreateStaffingContactRequest(
    string Name, string? Title, string? Email, bool EmailVerified, string? LinkedInUrl, string? Evidence);

public sealed record CreateStaffingDealRequest(
    Guid? ContactId, string Title, StaffingDealSource Source, string? ExternalReference, decimal? EstimatedValue,
    string? Currency, string? NextAction, DateTime? NextActionAt);

public sealed record UpdateStaffingDealRequest(
    string Title, string? ExternalReference, decimal? EstimatedValue, string? Currency, string? NextAction,
    DateTime? NextActionAt, int ExpectedVersion);

public sealed record MoveStaffingDealRequest(StaffingDealStage Stage, int ExpectedVersion);
public sealed record AddStaffingDealNoteRequest(string Detail);
