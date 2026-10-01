using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace OpportunityPilot.Api.Auth;

/// <summary>
/// Used when no sign-in provider is configured: nobody is ever authenticated, and protected endpoints
/// answer 503 "Setup required" rather than 401, so the web app does not mistake it for an expired session.
/// </summary>
public sealed class SetupRequiredAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SetupRequired";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await Context.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Setup required",
                Detail = "Sign-in is not configured on the server. Set Auth__SupabaseUrl. See docs/SETUP.md."
            }
        });
    }
}
