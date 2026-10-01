using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Configuration;

namespace OpportunityPilot.Application.Capabilities;

/// <summary>
/// Plain status labels. "Ready" is reserved for something this build can actually do;
/// "NotBuilt" marks a planned capability whose milestone has not been implemented yet.
/// </summary>
public enum CapabilityStatus
{
    Ready,
    Configured,
    NotConfigured,
    Disabled,
    ManualHandoff,
    NotBuilt
}

public sealed record CapabilityDto(string Key, string Name, string Category, CapabilityStatus Status, string Detail, string[] Can, string[] Cannot);

public sealed record CapabilitiesDto(string Environment, string DatabaseProvider, string AiMode, IReadOnlyList<CapabilityDto> Items);

public sealed class CapabilityService(
    IOptions<FeatureOptions> features,
    IOptions<AiOptions> ai,
    IOptions<DatabaseOptions> database)
{
    public CapabilitiesDto Get(string environment)
    {
        var f = features.Value;
        var a = ai.Value;
        var geminiKeyPresent = f.GeminiEnabled && !string.IsNullOrWhiteSpace(a.GeminiApiKey);

        var items = new List<CapabilityDto>
        {
            new("database", "Database", "Core", CapabilityStatus.Ready,
                $"{database.Value.Provider} is the active relational store.",
                ["Store profiles owned by your account"], []),

            new("gemini", "Gemini API", "AI",
                geminiKeyPresent ? CapabilityStatus.Configured : CapabilityStatus.NotConfigured,
                geminiKeyPresent
                    ? $"Server key present for {a.GeminiModel}. Live calls arrive in milestone M4 and are unverified; rules/templates mode applies until then."
                    : "No server-side key. Rules/templates mode applies. The key is set in server configuration and is never returned to the browser.",
                [], ["Parse goals", "Extract facts", "Summarise matches", "Draft outreach"]),

            new("rules", "Rules and templates", "AI", CapabilityStatus.NotBuilt,
                "Deterministic fallback for extraction, scoring and drafts. Arrives with research (M3) and outreach (M5).",
                [], ["Score candidates", "Fill draft templates"]),

            new("csv-import", "CSV and pasted text", "Sources", CapabilityStatus.NotBuilt,
                "Import with preview and row-level errors. Milestone M2.", [], ["Import companies"]),

            new("public-urls", "Public company URLs", "Sources", CapabilityStatus.NotBuilt,
                "Safe fetch with private-address blocking. Milestone M3.", [], ["Enrich supplied candidates"]),

            new("feeds", "Permitted RSS/Atom feeds", "Sources", CapabilityStatus.NotBuilt,
                "Discovers only what a permitted feed contains. Milestone M3.", [], ["Discover candidates"]),

            new("gmail", "Gmail", "Outreach",
                f.GmailEnabled ? CapabilityStatus.NotBuilt : CapabilityStatus.Disabled,
                "OAuth drafts and reviewed sending. Milestone M6. Nothing is sent from this build.",
                [], ["Create drafts", "Send approved messages"]),

            new("linkedin", "LinkedIn", "Platforms", CapabilityStatus.ManualHandoff,
                "No approved API access for this use.",
                ["Copy a prepared message", "Open a profile or job link"], ["Send messages", "Apply for you"]),

            new("naukri", "Naukri", "Platforms", CapabilityStatus.ManualHandoff,
                "No verified API access for this use.",
                ["Open the real application link"], ["Submit an application for you"]),

            new("instahyre", "InstaHyre", "Platforms", CapabilityStatus.ManualHandoff,
                "No verified API access for this use.",
                ["Open the real application link"], ["Respond on your behalf"]),

            new("mongo-archive", "MongoDB research archive", "Optional",
                f.MongoArchiveEnabled ? CapabilityStatus.NotBuilt : CapabilityStatus.Disabled,
                "Optional archive of bounded research metadata. Milestone M8.", [], ["Archive raw research"]),

            new("scheduler", "Scheduled research", "Optional", CapabilityStatus.NotConfigured,
                "Schedule setup required. Unattended runs are never claimed until a trigger is verified.",
                [], ["Run research while the app is closed"])
        };

        return new CapabilitiesDto(environment, database.Value.Provider, geminiKeyPresent ? "Gemini (key present)" : "Rules", items);
    }
}
