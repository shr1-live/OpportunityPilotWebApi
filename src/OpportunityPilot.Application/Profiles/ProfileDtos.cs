using System.Text.Json;
using OpportunityPilot.Domain.Profiles;

namespace OpportunityPilot.Application.Profiles;

public sealed record ProfileSummaryDto(Guid Id, ProfileType Type, string Name, int Version, DateTime? ConfirmedAt, DateTime UpdatedAt);

public sealed record ProfileDto(
    Guid Id,
    ProfileType Type,
    string Name,
    JsonElement Data,
    int Version,
    DateTime? ConfirmedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateProfileRequest(ProfileType Type, string Name, JsonElement? Data, bool Confirmed);

/// <summary>ExpectedVersion must match the stored version, otherwise the edit is rejected with 409.</summary>
public sealed record UpdateProfileRequest(string Name, JsonElement? Data, bool Confirmed, int ExpectedVersion);
