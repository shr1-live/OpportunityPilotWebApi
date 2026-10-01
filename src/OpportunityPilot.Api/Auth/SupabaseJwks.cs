using Microsoft.IdentityModel.Tokens;

namespace OpportunityPilot.Api.Auth;

/// <summary>
/// Caches the Supabase project's JSON Web Key Set ({SupabaseUrl}/auth/v1/.well-known/jwks.json).
/// Refreshes every 10 minutes, or sooner when a token arrives with an unknown key id (rate-limited to once per 30s).
/// </summary>
public sealed class SupabaseJwks(HttpClient http, string jwksUrl, ILogger<SupabaseJwks> logger)
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(30);
    private readonly Lock _gate = new();
    private IList<SecurityKey> _keys = [];
    private DateTime _fetchedAt = DateTime.MinValue;

    public IEnumerable<SecurityKey> GetKeys(string? kid)
    {
        lock (_gate)
        {
            var age = DateTime.UtcNow - _fetchedAt;
            var unknownKid = kid is not null && _keys.All(k => k.KeyId != kid);
            if (age > MaxAge || (unknownKid && age > MinRefreshInterval)) Refresh();
            return kid is null ? _keys : _keys.Where(k => k.KeyId == kid);
        }
    }

    private void Refresh()
    {
        try
        {
            using var response = http.Send(new HttpRequestMessage(HttpMethod.Get, jwksUrl));
            response.EnsureSuccessStatusCode();
            using var reader = new StreamReader(response.Content.ReadAsStream());
            _keys = new JsonWebKeySet(reader.ReadToEnd()).GetSigningKeys();
            _fetchedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            // Keep the previous keys; tokens signed by an unknown key will fail validation.
            logger.LogWarning(ex, "Could not refresh Supabase JWKS");
            _fetchedAt = DateTime.UtcNow - MaxAge + MinRefreshInterval;
        }
    }
}
