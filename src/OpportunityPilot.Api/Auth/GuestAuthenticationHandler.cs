using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Auth;

namespace OpportunityPilot.Api.Auth;

/// <summary>Authenticates opaque guest bearer tokens through their persisted SHA-256 hash.</summary>
public sealed class GuestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Guest";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = authorization["Bearer ".Length..].Trim();
        if (!token.StartsWith(GuestSessionService.TokenPrefix, StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        var sessions = Context.RequestServices.GetRequiredService<GuestSessionService>();
        var ownerId = await sessions.ValidateAsync(token, Context.RequestAborted);
        if (ownerId is null) return AuthenticateResult.Fail("Guest session is unknown or expired.");

        var identity = new ClaimsIdentity(
            [new Claim("sub", ownerId.Value.ToString()), new Claim("guest", "true")], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
