namespace OpportunityPilot.Application.Configuration;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>SqlServer (local LocalDB development) or Postgres (Supabase cloud and integration tests).</summary>
    public string Provider { get; set; } = "SqlServer";
}

public sealed class FeatureOptions
{
    public const string Section = "Features";
    public bool GeminiEnabled { get; set; }
    public bool MongoArchiveEnabled { get; set; }
    public bool GmailEnabled { get; set; }
}

public sealed class AiOptions
{
    public const string Section = "Ai";
    public string Mode { get; set; } = "Rules";
    public string? GeminiApiKey { get; set; }
    public string GeminiModel { get; set; } = "gemini-3.8-flash";
    public bool AllowPaidUsage { get; set; }
    public int MaxCallsPerRun { get; set; } = 12;
    public int MaxOutputTokens { get; set; } = 1500;
    public int DailyCallLimit { get; set; } = 200;
    public int TimeoutSeconds { get; set; } = 20;
}

/// <summary>
/// Adzuna job search (free developer keys from developer.adzuna.com). Both values are server-side secrets: they are
/// never logged, returned, or written into research events (the request URL that carries them is never recorded).
/// Without both, Adzuna sources fail safely and the capability reports NotConfigured.
/// </summary>
public sealed class AdzunaOptions
{
    public const string Section = "Adzuna";
    public string? AppId { get; set; }
    public string? AppKey { get; set; }

    public bool Configured => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppKey);
}

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>Supabase project URL, e.g. https://abc.supabase.co. Issuer is {SupabaseUrl}/auth/v1.</summary>
    public string? SupabaseUrl { get; set; }

    /// <summary>Supabase access tokens use "authenticated".</summary>
    public string Audience { get; set; } = "authenticated";

    /// <summary>Only for legacy Supabase projects still signing with the shared HS256 secret. Prefer JWKS.</summary>
    public string? LegacyJwtSecret { get; set; }

    /// <summary>
    /// Keep "Continue as guest" next to real accounts once SupabaseUrl is set (default true). Guests always get their
    /// own isolated identity; set false to require an account.
    /// </summary>
    public bool AllowGuests { get; set; } = true;

    /// <summary>Local development only. Startup fails if this is true outside the Development environment.</summary>
    public bool DevBypass { get; set; }
}
