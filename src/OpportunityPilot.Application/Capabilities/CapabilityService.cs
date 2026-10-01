using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Configuration;

namespace OpportunityPilot.Application.Capabilities;

/// <summary>
/// Plain status labels. "Ready" is reserved for something this build can actually do;
/// "NotBuilt" marks a planned capability whose milestone has not been implemented yet;
/// "LocalAgent" means the work happens in the user's own browser via the desktop agent, not on this server.
/// </summary>
public enum CapabilityStatus
{
    Ready,
    Configured,
    NotConfigured,
    Disabled,
    ManualHandoff,
    NotBuilt,
    LocalAgent
}

public sealed record CapabilityDto(string Key, string Name, string Category, CapabilityStatus Status, string Detail, string[] Can, string[] Cannot);

/// <param name="SetupRequired">Plain-language list of missing configuration; empty when the deployment is complete.</param>
/// <param name="GuestSignIn">Demo mode: no sign-in provider, so visitors continue as random guests.</param>
/// <param name="TemporaryStorage">Demo mode: no database, so data is kept in memory until the server restarts.</param>
public sealed record CapabilitiesDto(
    string Environment, string DatabaseProvider, string AiMode,
    IReadOnlyList<string> SetupRequired, bool GuestSignIn, bool TemporaryStorage,
    IReadOnlyList<CapabilityDto> Items);

public sealed class CapabilityService(
    IOptions<FeatureOptions> features,
    IOptions<AiOptions> ai,
    IOptions<DatabaseOptions> database,
    SetupState setup)
{
    public CapabilitiesDto Get(string environment)
    {
        var f = features.Value;
        var a = ai.Value;
        var geminiKeyPresent = f.GeminiEnabled && !string.IsNullOrWhiteSpace(a.GeminiApiKey);

        var items = new List<CapabilityDto>
        {
            setup.DatabaseConfigured
                ? new("database", "Database", "Core", CapabilityStatus.Ready,
                    $"{database.Value.Provider} is the active relational store.",
                    ["Store profiles owned by your account"], [])
                : new("database", "Database", "Core", CapabilityStatus.NotConfigured,
                    "Demo mode: no database connection string (ConnectionStrings__Main), so data is kept in memory and resets when the server restarts.",
                    ["Store profiles until the server restarts"], ["Keep data across restarts"]),

            setup.AuthConfigured
                ? new("auth", "Sign-in", "Core", CapabilityStatus.Ready,
                    "Requests are accepted only with a valid signed token.", ["Keep each account's data separate"], [])
                : new("auth", "Sign-in", "Core", CapabilityStatus.NotConfigured,
                    "Demo mode: no Supabase project URL (Auth__SupabaseUrl), so each browser continues as a random guest. Guests cannot see each other's data.",
                    ["Continue as a guest"], ["Create accounts", "Sign in from another device"]),

            new("gemini", "Gemini API", "AI",
                geminiKeyPresent ? CapabilityStatus.Configured : CapabilityStatus.NotConfigured,
                geminiKeyPresent
                    ? $"Server key present for {a.GeminiModel}. Live calls arrive in milestone M4 and are unverified; rules/templates mode applies until then."
                    : "No server-side key. Rules/templates mode applies. The key is set in server configuration and is never returned to the browser.",
                [], ["Parse goals", "Extract facts", "Summarise matches", "Draft outreach"]),

            new("rules", "Rules and templates", "AI", CapabilityStatus.Ready,
                "Deterministic extraction and transparent scoring for Job and Customer campaigns.",
                ["Extract skills, experience range, location and work mode", "Score candidates with a per-criterion breakdown, coverage and gaps"],
                ["Understand needs beyond the keywords you configure", "Score Partner, Investor or Freelance campaigns yet", "Fill draft templates (M5)"]),

            new("csv-import", "CSV and pasted text", "Sources", CapabilityStatus.Ready,
                "Import with preview and row-level errors.",
                ["Import job postings or companies from CSV (1 MB, 1000 rows)", "Paste postings or company notes"],
                ["Read Excel files or PDFs"]),

            new("public-urls", "Public company URLs", "Sources", CapabilityStatus.Ready,
                "Safe fetch with private-address blocking; pages that need a login or JavaScript are marked for manual input.",
                ["Read public pages you supply"], ["Read pages behind a login", "Run JavaScript", "Reach private or internal addresses"]),

            new("feeds", "Permitted RSS/Atom feeds", "Sources", CapabilityStatus.Ready,
                "Discovers only what a permitted RSS/Atom feed contains.",
                ["Read RSS 2.0 and Atom feeds you supply (up to 100 entries)"], ["Search the web"]),

            new("gmail", "Gmail", "Outreach",
                f.GmailEnabled ? CapabilityStatus.NotBuilt : CapabilityStatus.Disabled,
                "OAuth drafts and reviewed sending. Milestone M6. Nothing is sent from this build.",
                [], ["Create drafts", "Send approved messages"]),

            new("linkedin", "LinkedIn", "Platforms", CapabilityStatus.LocalAgent,
                "Applies through your own logged-in browser on your computer with the OpportunityPilot agent. Unofficial automation: LinkedIn may restrict accounts that use it.",
                ["Search jobs with your filters", "Fill and submit Easy Apply forms from your saved answers", "Record each application here"],
                ["Answer questions your saved answers do not cover", "Get past security checks — the agent stops and asks you"]),

            new("naukri", "Naukri", "Platforms", CapabilityStatus.LocalAgent,
                "Applies through your own logged-in browser on your computer with the OpportunityPilot agent. Unofficial automation: Naukri may restrict accounts that use it.",
                ["Search jobs with your filters", "Apply and answer the recruiter questionnaire from your saved answers", "Record each application here"],
                ["Apply on company websites", "Answer questions your saved answers do not cover"]),

            new("instahyre", "InstaHyre", "Platforms", CapabilityStatus.ManualHandoff,
                "Not supported by the agent yet.",
                ["Open the real application link"], ["Apply for you"]),

            new("mongo-archive", "MongoDB research archive", "Optional",
                f.MongoArchiveEnabled ? CapabilityStatus.NotBuilt : CapabilityStatus.Disabled,
                "Optional archive of bounded research metadata. Milestone M8.", [], ["Archive raw research"]),

            new("scheduler", "Scheduled research", "Optional", CapabilityStatus.NotConfigured,
                "Schedule setup required. Unattended runs are never claimed until a trigger is verified.",
                [], ["Run research while the app is closed"])
        };

        return new CapabilitiesDto(
            environment,
            setup.DatabaseConfigured ? database.Value.Provider : "In-memory (temporary)",
            geminiKeyPresent ? "Gemini (key present)" : "Rules",
            setup.Missing, !setup.AuthConfigured, !setup.DatabaseConfigured, items);
    }
}
