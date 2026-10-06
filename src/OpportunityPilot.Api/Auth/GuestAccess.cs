namespace OpportunityPilot.Api.Auth;

/// <summary>Whether provider-independent guest login is available on this deployment.</summary>
public sealed record GuestAccess(bool Enabled);
