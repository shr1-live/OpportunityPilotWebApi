using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Configuration;

namespace OpportunityPilot.Api.Auth;

public static class AuthSetup
{
    public static void AddOpportunityPilotAuth(this WebApplicationBuilder builder, SetupState setup)
    {
        var auth = builder.Configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
        var devBypass = auth.DevBypass;

        // A security guard, not a setup gap: this must stop the process.
        if (devBypass && !builder.Environment.IsDevelopment())
            throw new InvalidOperationException("Auth:DevBypass is only allowed in the Development environment.");

        var baseUrl = auth.SupabaseUrl?.Trim().TrimEnd('/');
        var supabaseConfigured = !string.IsNullOrEmpty(baseUrl);
        if (supabaseConfigured && !baseUrl!.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            supabaseConfigured = false;
            setup.AuthMissing("Auth__SupabaseUrl must be an https URL.");
        }
        else if (!supabaseConfigured && !devBypass)
        {
            setup.AuthMissing("Auth__SupabaseUrl is not set.");
        }

        var schemes = new List<string>();
        var authBuilder = builder.Services.AddAuthentication(
            supabaseConfigured ? JwtBearerDefaults.AuthenticationScheme
            : devBypass ? DevBypassAuthenticationHandler.SchemeName
            : SetupRequiredAuthenticationHandler.SchemeName);

        if (!supabaseConfigured && !devBypass)
        {
            // Fail closed: start so health and capabilities can explain the gap, but authenticate no one.
            authBuilder.AddScheme<AuthenticationSchemeOptions, SetupRequiredAuthenticationHandler>(SetupRequiredAuthenticationHandler.SchemeName, null);
            schemes.Add(SetupRequiredAuthenticationHandler.SchemeName);
        }

        if (supabaseConfigured)
        {

            builder.Services.AddSingleton(sp => new SupabaseJwks(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SupabaseJwks)),
                $"{baseUrl}/auth/v1/.well-known/jwks.json",
                sp.GetRequiredService<ILogger<SupabaseJwks>>()));

            authBuilder.AddJwtBearer();
            builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<SupabaseJwks>((o, jwks) =>
                {
                    o.MapInboundClaims = false;
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = $"{baseUrl}/auth/v1",
                        ValidAudience = auth.Audience,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        RequireSignedTokens = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                        IssuerSigningKeyResolver = (_, _, kid, _) =>
                        {
                            var keys = jwks.GetKeys(kid).ToList();
                            if (!string.IsNullOrWhiteSpace(auth.LegacyJwtSecret))
                                keys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.LegacyJwtSecret)));
                            return keys;
                        }
                    };
                });
            schemes.Add(JwtBearerDefaults.AuthenticationScheme);
        }

        if (devBypass)
        {
            authBuilder.AddScheme<AuthenticationSchemeOptions, DevBypassAuthenticationHandler>(DevBypassAuthenticationHandler.SchemeName, null);
            schemes.Add(DevBypassAuthenticationHandler.SchemeName);
        }

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder(schemes.ToArray()).RequireAuthenticatedUser().Build());

        builder.Services.AddHttpClient(nameof(SupabaseJwks), c => c.Timeout = TimeSpan.FromSeconds(10));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    }
}
