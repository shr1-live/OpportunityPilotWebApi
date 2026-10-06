using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Agents;

namespace OpportunityPilot.Api.Auth;

/// <summary>
/// The local desktop agent authenticates with a personal key in the X-Agent-Key header. This scheme is not part
/// of the fallback policy, so a key only works on endpoints that explicitly allow the local-agent scheme.
/// </summary>
public sealed class AgentKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AgentKey";
    public const string Header = "X-Agent-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presented = Request.Headers[Header].ToString().Trim();
        if (string.IsNullOrEmpty(presented)) return AuthenticateResult.NoResult();

        var keys = Context.RequestServices.GetRequiredService<AgentKeyService>();
        var ownerId = await keys.ValidateAsync(presented, Context.RequestAborted);
        if (ownerId is null) return AuthenticateResult.Fail("Agent key is unknown or revoked.");

        var identity = new ClaimsIdentity([new Claim("sub", ownerId.Value.ToString()), new Claim("agent", "true")], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
