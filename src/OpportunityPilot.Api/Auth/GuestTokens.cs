using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace OpportunityPilot.Api.Auth;

/// <summary>
/// Demo-mode sign-in, active only while no Supabase project is configured. The server picks a random
/// identity for each guest and signs it, so a guest cannot choose or guess someone else's identity.
/// Without Auth:GuestSigningKey the key is generated per process, so guest sessions end on restart.
/// </summary>
public sealed class GuestTokens
{
    public const string SchemeName = "Guest";
    public const string Issuer = "opportunitypilot-guest";
    public const string Audience = "opportunitypilot";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public GuestTokens(bool enabled, string? configuredKey)
    {
        Enabled = enabled;
        Key = new SymmetricSecurityKey(configuredKey is { Length: >= 32 }
            ? Encoding.UTF8.GetBytes(configuredKey)
            : RandomNumberGenerator.GetBytes(64));
    }

    public bool Enabled { get; }
    public SymmetricSecurityKey Key { get; }

    public (string Token, DateTime ExpiresAt) Issue(DateTime now)
    {
        var expires = now + Lifetime;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim("guest", "true")]),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
        });
        return (token, expires);
    }

    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = Key,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        RequireSignedTokens = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
}
