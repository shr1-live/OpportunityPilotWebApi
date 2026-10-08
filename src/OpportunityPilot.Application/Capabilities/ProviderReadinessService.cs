using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Application.Configuration;
using OpportunityPilot.Domain.Applications;
using OpportunityPilot.Domain.Outreach;
using OpportunityPilot.Domain.Wellfound;

namespace OpportunityPilot.Application.Capabilities;

/// <summary>How far the app goes for one step with a provider: it does it, it prepares it for you, you do it, or not at all.</summary>
public enum Support { Automatic, Assisted, Manual, None }

/// <param name="Credential">The server setting the provider needs, or null when none.</param>
/// <param name="LastVerified">The latest stored proof that this provider worked for you (never assumed); null when there is none.</param>
public sealed record ProviderReadinessDto(
    string Key, string Name, IReadOnlyList<string> Workspaces, Support Discovery, Support Drafting, Support Approval, Support Execution,
    string? Credential, bool CredentialSet, string ManualStep, string RiskNote, DateTime? LastVerified, string? LastVerifiedWhat, string Tasks);

/// <summary>P7: the provider readiness and compliance matrix. Every claim is either configuration or a stored record.</summary>
public sealed class ProviderReadinessService(
    IAppDbContext db, ICurrentUser user, OperationalMetrics metrics,
    IOptions<FeatureOptions> features, IOptions<JsearchOptions> jsearch, IOptions<AdzunaOptions> adzuna)
{
    public async Task<IReadOnlyList<ProviderReadinessDto>> ListAsync(CancellationToken ct)
    {
        var owner = user.OwnerId;
        async Task<DateTime?> LastApplied(ApplicationPlatform p) =>
            await db.JobApplications.Where(a => a.OwnerId == owner && a.Platform == p && a.Status == ApplicationStatus.Applied)
                .OrderByDescending(a => a.OccurredAt).Select(a => (DateTime?)a.OccurredAt).FirstOrDefaultAsync(ct);
        var lastManualSend = await db.ProviderExecutions.Where(e => e.OwnerId == owner && e.Subject == ExecutionSubject.OutreachDraft && e.State == ExecutionState.Succeeded)
            .OrderByDescending(e => e.CompletedAt).Select(e => e.CompletedAt).FirstOrDefaultAsync(ct);
        var lastBid = await db.ProviderExecutions.Where(e => e.OwnerId == owner && e.Subject == ExecutionSubject.SalesBid && e.State == ExecutionState.Succeeded)
            .OrderByDescending(e => e.CompletedAt).Select(e => e.CompletedAt).FirstOrDefaultAsync(ct);
        var lastWellfound = await db.WellfoundActivities.Where(a => a.OwnerId == owner && a.Kind == WellfoundActivityKind.SyncObserved)
            .OrderByDescending(a => a.OccurredAt).Select(a => (DateTime?)a.OccurredAt).FirstOrDefaultAsync(ct);
        var jsearchStats = metrics.Snapshot().FirstOrDefault(s => s.Operation == "jsearch.search");
        DateTime? jsearchOk = jsearchStats is { Count: > 0 } j && j.Count > j.Failures ? metrics.StartedAt : null;
        var linkedIn = await LastApplied(ApplicationPlatform.LinkedIn);
        var naukri = await LastApplied(ApplicationPlatform.Naukri);
        var instahyre = await LastApplied(ApplicationPlatform.Instahyre);

        string[] both = ["Candidate", "Sales"];
        return
        [
            new("job-boards", "Indeed, LinkedIn, SEEK postings (JSearch)", both, Support.Automatic, Support.None, Support.None, Support.None,
                "Jsearch__Key", jsearch.Value.Configured,
                "Apply or contact on the board itself; OpportunityPilot only finds and scores postings.",
                "Licensed aggregator; Indeed/LinkedIn/SEEK pages are never scraped.", jsearchOk,
                jsearchOk is null ? null : "a live search succeeded since the API started", "W10–W13, W18–W20"),
            new("wellfound", "Wellfound", both, Support.Automatic, Support.None, Support.Manual, Support.Manual, null, true,
                "Save or apply on Wellfound; recruiter data needs Wellfound Recruit OAuth.",
                "Only the anonymous public jobs page is read.", lastWellfound, lastWellfound is null ? null : "public jobs imported", "W7–W9, W1"),
            new("linkedin-agent", "LinkedIn Easy Apply (local agent)", ["Candidate"], Support.Assisted, Support.Assisted, Support.Automatic, Support.Assisted,
                "Agent key (Applications → Agent setup)", true, "Runs in your own logged-in browser on your computer; stops at questions it cannot answer.",
                "Unofficial automation: LinkedIn may restrict accounts that use it.", linkedIn, linkedIn is null ? null : "the agent reported an application", "U3"),
            new("naukri-agent", "Naukri (local agent)", ["Candidate"], Support.Assisted, Support.Assisted, Support.Automatic, Support.Assisted,
                "Agent key", true, "Runs in your own browser; answers the recruiter questionnaire from your saved answers.",
                "Unofficial automation: Naukri may restrict accounts that use it.", naukri, naukri is null ? null : "the agent reported an application", "U3"),
            new("instahyre-agent", "InstaHyre (local agent)", ["Candidate"], Support.Assisted, Support.None, Support.Automatic, Support.Assisted,
                "Agent key", true, "Collects personalised opportunities and applies only to your shortlist, in your own browser.",
                "Selectors need a first live run (U3); stops before unknown forms or security checks.", instahyre,
                instahyre is null ? null : "the agent reported an application", "U3"),
            new("email", "Email (send yourself; Gmail API not connected)", ["Sales", "Candidate"], Support.None, Support.Automatic, Support.Automatic,
                features.Value.GmailEnabled ? Support.Manual : Support.Manual, "Gmail OAuth (N6)", false,
                "Copy the approved text into your mailbox, send it, record the sent item as the receipt.",
                "Suppression list and exact-version approval are enforced before every send.", lastManualSend,
                lastManualSend is null ? null : "you confirmed a send with a receipt", "N6, S13"),
            new("linkedin-messages", "LinkedIn messages and connection notes", ["Sales"], Support.Manual, Support.Automatic, Support.Automatic, Support.Manual,
                null, true, "Paste the approved note into LinkedIn yourself, then record that you sent it.",
                "Never sent automatically; LinkedIn is not scraped server-side.", null, null, "S15, X5"),
            new("freelancer", "Freelancer.com projects and bids", ["Sales"], Support.Manual, Support.Automatic, Support.Automatic, Support.Manual,
                "Freelancer.com OAuth (N5)", false, "Import the project, approve the exact bid, place it on Freelancer yourself, record the bid reference.",
                "Bids are never placed without your approval of the exact amount, timing and text.", lastBid,
                lastBid is null ? null : "you confirmed a placed bid", "N5"),
            new("upwork", "Upwork projects and proposals", ["Sales"], Support.Manual, Support.Automatic, Support.Automatic, Support.Manual,
                "Upwork__ClientId / Upwork__ClientSecret (U6)", false, "Approved key needed for search; proposals are submitted by you and never spend Connects silently.",
                "Upwork must approve the app; RSS feeds were retired in 2024.", null, null, "W16, S14"),
            new("tenders", "Tender and RFP portals", ["Sales"], Support.Manual, Support.Automatic, Support.Automatic, Support.Manual, null, true,
                "Add the tender as a project with its notice as evidence; submit on the portal yourself.",
                "Portal submissions are always manual.", lastBid, lastBid is null ? null : "you confirmed a placed bid", "N5"),
            new("contact-forms", "Company contact forms", ["Sales"], Support.None, Support.Automatic, Support.Automatic, Support.Manual, null, true,
                "Paste the approved text into the company's form yourself and record it.",
                "Forms are never submitted automatically.", lastManualSend, lastManualSend is null ? null : "you confirmed a send with a receipt", "P6"),
            new("adzuna", "Adzuna job search", ["Candidate"], Support.Automatic, Support.None, Support.None, Support.None,
                "Adzuna__AppId / Adzuna__AppKey", adzuna.Value.Configured, "Apply on the employer's page.", "Official API with free developer keys.",
                null, null, "U2"),
        ];
    }
}
