using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OpportunityPilot.Api.Auth;

/// <summary>
/// Local development only: the caller names a synthetic user in the X-Dev-User header and gets a
/// stable owner id derived from it. Program.cs refuses to start if this is enabled outside Development.
/// </summary>
public sealed class DevBypassAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevBypass";
    public const string Header = "X-Dev-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var name = Request.Headers[Header].ToString().Trim();
        if (string.IsNullOrEmpty(name)) return Task.FromResult(AuthenticateResult.NoResult());
        if (name.Length > 100) return Task.FromResult(AuthenticateResult.Fail("Dev user name too long."));

        var ownerId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("dev:" + name.ToLowerInvariant()))[..16]);
        var identity = new ClaimsIdentity(
            [new Claim("sub", ownerId.ToString()), new Claim("email", name), new Claim("dev", "true")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
