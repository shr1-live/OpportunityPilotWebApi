using OpportunityPilot.Application.Abstractions;

namespace OpportunityPilot.Api.Auth;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid OwnerId
    {
        get
        {
            var sub = accessor.HttpContext?.User.FindFirst("sub")?.Value;
            return Guid.TryParse(sub, out var id) && id != Guid.Empty
                ? id
                : throw new UnauthorizedAccessException("Authenticated subject is missing or not a UUID.");
        }
    }
}
