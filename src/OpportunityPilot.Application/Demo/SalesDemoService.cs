using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Application.Drafts;
using OpportunityPilot.Application.Opportunities;
using OpportunityPilot.Application.Outreach;
using OpportunityPilot.Application.Profiles;
using OpportunityPilot.Application.Research;
using OpportunityPilot.Application.Sources;
using OpportunityPilot.Application.Staffing;
using OpportunityPilot.Domain.Common;
using OpportunityPilot.Domain.Drafts;
using OpportunityPilot.Domain.Opportunities;
using OpportunityPilot.Domain.Outreach;
using OpportunityPilot.Domain.Profiles;
using OpportunityPilot.Domain.Research;
using OpportunityPilot.Domain.Staffing;

namespace OpportunityPilot.Application.Demo;

public sealed record SalesDemoStatus(bool Exists, bool ResearchDone, int Campaigns, int Opportunities, int Shortlisted, int Drafts, int StaffingDeals, IReadOnlyList<Guid> JobIds);

/// <summary>
/// A replayable Sales demo (P8) and staffing demo (X15) built only through the app's own services, so every record is
/// validated, scored and evidenced like real data. Everything it creates is named "Demo — …" and every company in it is
/// fictional (.example domains). Reset removes exactly those records of the signed-in owner.
/// </summary>
public sealed class SalesDemoService(
    IAppDbContext db, ICurrentUser user, ProfileService profiles, CampaignService campaigns, SourceService sources,
    ResearchService research, OpportunityService opportunities, DraftService drafts, OutreachService outreach,
    StaffingService staffing, StaffingPipelineService pipeline, TimeProvider clock)
{
    public const string Prefix = "Demo — ";

    private static readonly (OpportunityMode Mode, string Name, string Goal, CampaignCriteria Criteria, string Companies)[] Campaigns =
    [
        (OpportunityMode.Customer, "Customers: logistics firms with dispatch pain", "Mid-sized logistics companies that still plan routes by hand.",
            new CampaignCriteria { Keywords = ["route planning"], Industries = ["Logistics"], Problems = ["manual dispatch"], Signals = ["hiring dispatchers"], Locations = ["Germany"] },
            Companies("Kranich Freight GmbH", "Logistics", "Germany", "Kranich runs 120 trucks and plans routes by hand in spreadsheets; manual dispatch slows every morning. They are hiring dispatchers.",
                      "Blue Harbour Shipping", "Logistics", "Netherlands", "Blue Harbour plans routes with an in-house tool and is expanding to Belgium.")),
        (OpportunityMode.Partner, "Partners: ERP integrators", "Systems integrators that implement ERP for logistics firms and could resell route planning.",
            new CampaignCriteria { Keywords = ["ERP integration"], Industries = ["Systems integration"], Problems = ["no routing module"], Signals = ["partner programme"], Locations = ["Germany"] },
            Companies("Nordlicht Systems", "Systems integration", "Germany", "Nordlicht implements ERP integration for logistics clients; their stack has no routing module and they run a partner programme.",
                      "Pixel & Pine Agency", "Marketing", "Austria", "A design agency building websites for retailers.")),
        (OpportunityMode.Investor, "Investors: seed logistics funds", "Seed funds in Europe that back B2B logistics software.",
            new CampaignCriteria { Keywords = ["logistics software"], Industries = ["Venture capital"], Problems = ["seed"], Signals = ["new fund"], Locations = ["Berlin"] },
            Companies("Spree Ventures", "Venture capital", "Berlin", "Spree Ventures writes seed cheques into logistics software and announced a new fund this year.",
                      "Harbour Growth Partners", "Private equity", "London", "Harbour Growth buys mature manufacturing businesses.")),
        (OpportunityMode.Freelance, "Freelance: .NET API projects", "Fixed-scope .NET API projects for small businesses.",
            new CampaignCriteria { Keywords = [".NET API"], Industries = ["Retail"], Problems = ["API integration"], Signals = ["fixed price"], Locations = ["Remote"] },
            Companies("Corner Shop Online (project brief)", "Retail", "Remote", "Fixed price project: build a .NET API integration between the shop and its warehouse, four weeks.",
                      "Weekend Photo Club (project brief)", "Hobby", "Remote", "Looking for someone to design a club logo.")),
    ];

    private static string Companies(params string[] f) =>
        string.Join("\n---\n", Enumerable.Range(0, f.Length / 4).Select(i =>
            $"{f[i * 4]}\nWebsite: https://{Slug(f[i * 4])}.example\nIndustry: {f[i * 4 + 1]}\nLocation: {f[i * 4 + 2]}\n{f[i * 4 + 3]}"));

    private static string Slug(string name) => new string(name.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());

    public async Task<SalesDemoStatus> StatusAsync(CancellationToken ct)
    {
        var demoCampaigns = await DemoCampaigns().Select(c => c.Id).ToListAsync(ct);
        var jobs = await db.ResearchJobs.Where(j => j.OwnerId == user.OwnerId && demoCampaigns.Contains(j.CampaignId)).ToListAsync(ct);
        var opps = await db.Opportunities.Where(o => o.OwnerId == user.OwnerId && demoCampaigns.Contains(o.CampaignId)).Select(o => new { o.Id, o.Status }).ToListAsync(ct);
        var oppIds = opps.Select(o => o.Id).ToList();
        return new(demoCampaigns.Count > 0,
            jobs.Count > 0 && jobs.All(j => j.State is ResearchJobState.Completed or ResearchJobState.CompletedWithGaps or ResearchJobState.Failed or ResearchJobState.Cancelled),
            demoCampaigns.Count, opps.Count, opps.Count(o => o.Status == OpportunityStatus.Shortlisted),
            await db.OutreachDrafts.CountAsync(d => d.OwnerId == user.OwnerId && oppIds.Contains(d.OpportunityId), ct),
            await db.StaffingDeals.CountAsync(d => d.OwnerId == user.OwnerId && d.Title.StartsWith(Prefix), ct),
            jobs.Select(j => j.Id).ToList());
    }

    /// <summary>Step 1: profile, four campaigns with fictional companies, research queued.</summary>
    public async Task<SalesDemoStatus> StartAsync(CancellationToken ct)
    {
        if (await DemoCampaigns().AnyAsync(ct)) throw new ConflictException("The Sales demo already exists. Reset it first to replay it.");
        var data = JsonSerializer.SerializeToElement(new
        {
            fields = new Dictionary<string, string>
            {
                ["offer"] = "RouteCo plans delivery routes automatically for logistics firms (fictional demo company).",
                ["idealCustomer"] = "Logistics firms with 50–500 vehicles that still dispatch by hand.",
                ["targetSectors"] = "Logistics in Germany and the Netherlands",
                ["proof"] = "Cut planning time by 70% at a fictional pilot customer (demo figure).",
                ["outreachIdentity"] = "Asha Rao, Head of Sales, RouteCo (demo)",
            },
            confirmations = new Dictionary<string, bool> { ["proof"] = true, ["capabilities"] = true },
        });
        var profile = await profiles.CreateAsync(new CreateProfileRequest(ProfileType.Product, Prefix + "RouteCo (fictional)", data, true), ct);
        foreach (var c in Campaigns)
        {
            var campaign = await campaigns.CreateAsync(new CreateCampaignRequest(profile.Id, c.Mode, Prefix + c.Name, c.Goal, c.Criteria, null, null), ct);
            await sources.CreateAsync(campaign.Id, new CreateSourceRequest(SourceKind.Paste, "Demo list (fictional companies)", null, c.Companies,
                "Fictional companies written for the demo."), ct);
            await research.QueueAsync(campaign.Id, ct);
        }
        return await StatusAsync(ct);
    }

    /// <summary>Step 2, after research ran: shortlist, drafts (one approved), follow-ups, and the staffing demo.</summary>
    public async Task<SalesDemoStatus> FinishAsync(CancellationToken ct)
    {
        var status = await StatusAsync(ct);
        if (!status.Exists) throw new ConflictException("Start the Sales demo first.");
        if (!status.ResearchDone) throw new ConflictException("The demo research is still running. Try again in a moment.");
        if (status.Shortlisted > 0) return status;

        var demoCampaigns = await DemoCampaigns().Select(c => new { c.Id, c.Mode }).ToListAsync(ct);
        var modes = demoCampaigns.ToDictionary(c => c.Id, c => c.Mode);
        var campaignIds = modes.Keys.ToList();
        var candidates = await db.Opportunities
            .Where(o => o.OwnerId == user.OwnerId && campaignIds.Contains(o.CampaignId) && o.Outcome != FilterOutcome.Excluded)
            .Select(o => new { o.Id, o.CampaignId, o.Score })
            .ToListAsync(ct);
        var best = candidates.GroupBy(o => o.CampaignId).Select(g => g.OrderByDescending(o => o.Score).First()).ToList();
        var first = true;
        foreach (var item in best)
        {
            var id = item.Id;
            await opportunities.UpdateStatusAsync(id, new UpdateOpportunityStatusRequest(OpportunityStatus.Shortlisted), ct);
            var channel = modes[item.CampaignId] switch
            {
                OpportunityMode.Partner => DraftChannel.LinkedInMessage,
                OpportunityMode.Freelance => DraftChannel.ContactForm,
                _ => DraftChannel.Email,
            };
            var recipient = channel == DraftChannel.Email ? $"hello@{id.ToString("N")[..8]}.example" : null;
            var draft = await drafts.CreateAsync(id, new CreateDraftRequest(channel, recipient), ct);
            if (first)
            {
                await drafts.ApproveAsync(draft.Id, new ApproveDraftRequest(draft.Version), ct);
                first = false;
            }
            await outreach.CreateNextActionAsync(id, new CreateNextActionRequest(NextActionKind.FollowUp, "Demo: check for a reply",
                clock.GetUtcNow().UtcDateTime.AddDays(3), "Europe/Berlin"), ct);
        }
        await StaffingDemoAsync(ct);
        return await StatusAsync(ct);
    }

    /// <summary>X15: a client deal with a consenting candidate submitted, an interview scheduled and a rate card.</summary>
    private async Task StaffingDemoAsync(CancellationToken ct)
    {
        var account = await staffing.CreateAccountAsync(new CreateStaffingAccountRequest(Prefix + "Kranich Freight GmbH (fictional)", StaffingAccountSource.Manual,
            "kranichfreight.example", "Logistics", "Hamburg", null), ct);
        var deal = await staffing.CreateDealAsync(account.Id, new CreateStaffingDealRequest(null, Prefix + "2 senior .NET engineers, 6 months", StaffingDealSource.Referral,
            null, 96000, "EUR", "Send the shortlist", clock.GetUtcNow().UtcDateTime.AddDays(2)), ct);
        foreach (var stage in new[] { StaffingDealStage.Qualified, StaffingDealStage.Shortlisted, StaffingDealStage.OutreachApproved, StaffingDealStage.Contacted,
                     StaffingDealStage.Replied, StaffingDealStage.MeetingScheduled, StaffingDealStage.RequirementConfirmed })
            deal = await staffing.MoveDealAsync(deal.Id, new MoveStaffingDealRequest(stage, deal.Version), ct);

        var candidate = await pipeline.SaveCandidateAsync(null, new SaveStaffingCandidateRequest(Prefix + "Priya S. (fictional)", "Senior .NET engineer",
            "priya@demo.example", null, "Pune", "C#, ASP.NET Core, Azure", 7, CandidateAvailability.NoticePeriod, 30, 45, "EUR", RateUnit.Hour,
            "Demo resume: seven years of C# and Azure work on logistics systems.", true, null), ct);
        candidate = await pipeline.RecordConsentAsync(candidate.Id, new RecordConsentRequest(CandidateConsent.Granted,
            ["Name", "Headline", "Skills", "Experience", "Rate", "Availability"], "Demo: consent recorded for the walkthrough", candidate.Version), ct);
        var submission = await pipeline.SaveSubmissionAsync(deal.Id, null, new SaveSubmissionRequest(candidate.Id, ["Name", "Headline", "Skills", "Rate", "Availability"],
            "Demo submission", null), ct);
        submission = await pipeline.ApproveSubmissionAsync(deal.Id, submission.Id, new VersionRequest(submission.Version), ct);
        submission = await pipeline.MarkSubmissionSentAsync(deal.Id, submission.Id, new MarkHandoffRequest(submission.Version, HandoffChannel.Email, "Demo: sent from the demo mailbox"), ct);
        var interview = await pipeline.RequestInterviewAsync(deal.Id, new RequestInterviewRequest(submission.Id), ct);
        interview = await pipeline.ScheduleInterviewAsync(deal.Id, interview.Id, new ScheduleInterviewRequest(clock.GetUtcNow().UtcDateTime.Date.AddDays(5).AddHours(9),
            "Asia/Kolkata", 45, InterviewMode.Video, "https://meet.demo.example/round1", "Technical round", "Demo internal note", interview.Version), ct);
        interview = await pipeline.RecordCandidateNotifiedAsync(deal.Id, interview.Id,
            new NotifyCandidateRequest(NotificationStatus.NotifiedManually, interview.Version), ct);
        interview = await pipeline.FinishInterviewAsync(deal.Id, interview.Id,
            new FinishInterviewRequest(InterviewState.Completed, interview.Version), ct);
        await pipeline.RecordFeedbackAsync(deal.Id, new RecordFeedbackRequest(submission.Id, interview.Id, FeedbackSource.Client,
            FeedbackDecision.Selected, "Demo: selected after the technical discussion.", true), ct);

        var offer = await pipeline.SaveOfferAsync(deal.Id, null, new SaveOfferRequest(submission.Id, 60, 45, "EUR", RateUnit.Hour,
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddDays(30)), 24000, "Fictional demo commercial terms.", null), ct);
        offer = await pipeline.MoveOfferAsync(deal.Id, offer.Id, new MoveOfferRequest(OfferState.Extended, offer.Version), ct);
        offer = await pipeline.MoveOfferAsync(deal.Id, offer.Id, new MoveOfferRequest(OfferState.Accepted, offer.Version), ct);
        offer = await pipeline.UpdateContractAsync(deal.Id, offer.Id, new UpdateContractRequest(ContractStatus.Signed,
            "DEMO-SOW-1", "Manual demo signature", "private-demo-document-reference", offer.Version), ct);
        await pipeline.RecordOutcomeAsync(deal.Id, offer.Id, new RecordOutcomeRequest(PlacementOutcome.Placed, offer.Version), ct);

        var card = await pipeline.SaveRateCardAsync(null, new SaveRateCardRequest(Prefix + "2026 delivery rates", "EUR",
            [new RateCardLine(".NET engineer", "Senior", RateUnit.Hour, 60), new RateCardLine("QA engineer", null, RateUnit.Hour, 40)], "Net 30 (demo)", null, null), ct);
        card = await pipeline.SetRateCardStatusAsync(card.Id, new RateCardStatusRequest(RateCardStatus.Active, card.Version), ct);
        var proposal = await pipeline.CreateProposalAsync(deal.Id, new CreateProposalRequest(card.Id, Prefix + "Kranich engineering team proposal",
            null, "Two senior .NET engineers for the confirmed six-month requirement (fictional demo).", DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddDays(14))), ct);
        await pipeline.ApproveProposalAsync(deal.Id, proposal.Id, new VersionRequest(proposal.Version), ct);
    }

    /// <summary>Removes only this owner's "Demo — " records; nothing else is touched.</summary>
    public async Task ResetAsync(CancellationToken ct)
    {
        var owner = user.OwnerId;
        var dealIds = await db.StaffingDeals.Where(d => d.OwnerId == owner && d.Title.StartsWith(Prefix)).Select(d => d.Id).ToListAsync(ct);
        db.StaffingFeedbackEntries.RemoveRange(await db.StaffingFeedbackEntries.Where(x => x.OwnerId == owner && dealIds.Contains(x.DealId)).ToListAsync(ct));
        db.StaffingInterviews.RemoveRange(await db.StaffingInterviews.Where(x => x.OwnerId == owner && dealIds.Contains(x.DealId)).ToListAsync(ct));
        db.StaffingOffers.RemoveRange(await db.StaffingOffers.Where(x => x.OwnerId == owner && dealIds.Contains(x.DealId)).ToListAsync(ct));
        db.StaffingSubmissions.RemoveRange(await db.StaffingSubmissions.Where(x => x.OwnerId == owner && dealIds.Contains(x.DealId)).ToListAsync(ct));
        db.StaffingProposals.RemoveRange(await db.StaffingProposals.Where(x => x.OwnerId == owner && dealIds.Contains(x.DealId)).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        db.StaffingDeals.RemoveRange(await db.StaffingDeals.Where(x => x.OwnerId == owner && dealIds.Contains(x.Id)).ToListAsync(ct));
        db.StaffingCandidates.RemoveRange(await db.StaffingCandidates.Where(x => x.OwnerId == owner && x.Name.StartsWith(Prefix)).ToListAsync(ct));
        db.StaffingRateCards.RemoveRange(await db.StaffingRateCards.Where(x => x.OwnerId == owner && x.Name.StartsWith(Prefix)).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        db.StaffingAccounts.RemoveRange(await db.StaffingAccounts.Where(x => x.OwnerId == owner && x.Name.StartsWith(Prefix)).ToListAsync(ct));
        var campaignIds = await DemoCampaigns().Select(c => c.Id).ToListAsync(ct);
        var oppIds = await db.Opportunities.Where(o => o.OwnerId == owner && campaignIds.Contains(o.CampaignId)).Select(o => o.Id).ToListAsync(ct);
        db.ProviderExecutions.RemoveRange(await db.ProviderExecutions.Where(x => x.OwnerId == owner &&
            db.OutreachDrafts.Any(d => d.Id == x.SubjectId && oppIds.Contains(d.OpportunityId))).ToListAsync(ct));
        // Opportunities first: their evidence links restrict deleting evidence, which campaigns cascade to.
        db.Opportunities.RemoveRange(await db.Opportunities.Where(o => o.OwnerId == owner && oppIds.Contains(o.Id)).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        db.Campaigns.RemoveRange(await db.Campaigns.Where(c => c.OwnerId == owner && campaignIds.Contains(c.Id)).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        db.Profiles.RemoveRange(await db.Profiles.Where(p => p.OwnerId == owner && p.Name.StartsWith(Prefix)).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Domain.Campaigns.Campaign> DemoCampaigns() => db.Campaigns.Where(c => c.OwnerId == user.OwnerId && c.Name.StartsWith(Prefix));
}
