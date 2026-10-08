using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpportunityPilot.IntegrationTests;

/// <summary>The staffing CRM from deal to placement through the real API and database, plus its consent and approval guards.</summary>
public class StaffingPipelineApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static async Task<(HttpClient Client, Guid DealId)> DealAtRequirementConfirmedAsync(PostgresApiFactory factory, string user)
    {
        var client = PostgresApiFactory.ClientFor(factory, user);
        var account = await (await client.PostAsJsonAsync("/api/v1/staffing/accounts", new { name = "Acme Logistics", source = "Manual" })).Json(HttpStatusCode.Created);
        var deal = await (await client.PostAsJsonAsync($"/api/v1/staffing/accounts/{account.Id()}/deals",
            new { title = "3 .NET engineers", source = "Manual", estimatedValue = 90000, currency = "USD" })).Json(HttpStatusCode.Created);
        var version = deal.Int("version");
        foreach (var stage in new[] { "Qualified", "Shortlisted", "OutreachApproved", "Contacted", "Replied", "MeetingScheduled", "RequirementConfirmed" })
            version = (await (await client.PostAsJsonAsync($"/api/v1/staffing/deals/{deal.Id()}/stage", new { stage, expectedVersion = version }))
                .Json(HttpStatusCode.OK)).Int("version");
        return (client, deal.Id());
    }

    private static async Task<JsonElement> CandidateWithConsentAsync(HttpClient client, string[] shareable)
    {
        var candidate = await (await client.PostAsJsonAsync("/api/v1/staffing/candidates", new
        {
            name = "Priya Sharma", headline = "Senior .NET engineer", email = "priya@example.test", phone = "+91 90000 00000",
            skills = "C#, ASP.NET Core, Azure", yearsExperience = 7, availability = "NoticePeriod", noticePeriodDays = 30,
            rateAmount = 45, rateCurrency = "USD", rateUnit = "Hour", resumeText = "Resume text", notifyByEmail = true
        })).Json(HttpStatusCode.Created);
        return await (await client.PostAsJsonAsync($"/api/v1/staffing/candidates/{candidate.Id()}/consent",
            new { consent = "Granted", shareableFields = shareable, evidence = "Email reply 8 Oct", expectedVersion = candidate.Int("version") }))
            .Json(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_deal_runs_from_submission_to_placement_and_the_kpis_count_it()
    {
        var (client, dealId) = await DealAtRequirementConfirmedAsync(factory, "staffing-flow@example.test");
        var candidate = await CandidateWithConsentAsync(client, ["Name", "Headline", "Skills", "Rate", "Availability", "Resume"]);
        var api = $"/api/v1/staffing/deals/{dealId}";

        var submission = await (await client.PostAsJsonAsync($"{api}/submissions",
            new { candidateId = candidate.Id(), sharedFields = new[] { "Name", "Skills", "Rate", "Availability" } })).Json(HttpStatusCode.OK);
        var snapshot = submission.GetProperty("snapshot");
        Assert.Equal("Priya Sharma", snapshot.Str("name"));
        Assert.Equal("45 USD per hour", snapshot.Str("rate"));
        Assert.False(snapshot.TryGetProperty("email", out _));

        submission = await (await client.PostAsJsonAsync($"{api}/submissions/{submission.Id()}/approve", new { expectedVersion = submission.Int("version") })).Json(HttpStatusCode.OK);
        submission = await (await client.PostAsJsonAsync($"{api}/submissions/{submission.Id()}/sent",
            new { expectedVersion = submission.Int("version"), channel = "Email", receipt = "Sent from sales@acme-staffing, msg 1842" })).Json(HttpStatusCode.OK);
        Assert.Equal("Sent", submission.Str("state"));
        Assert.Equal("CandidatesSubmitted", (await client.GetJson($"/api/v1/staffing/deals/{dealId}")).Str("stage"));

        var interview = await (await client.PostAsJsonAsync($"{api}/interviews", new { submissionId = submission.Id() })).Json(HttpStatusCode.OK);
        interview = await (await client.PostAsJsonAsync($"{api}/interviews/{interview.Id()}/schedule", new
        {
            scheduledAt = "2026-10-20T09:30:00Z", timeZone = "Asia/Kolkata", durationMinutes = 45, mode = "Video",
            location = "https://meet.example.test/abc", candidateNotes = "Tech round", internalNotes = "Client prefers Azure depth",
            expectedVersion = interview.Int("version")
        })).Json(HttpStatusCode.OK);
        Assert.Equal("NotNotified", interview.Str("candidateNotification"));
        interview = await (await client.PostAsJsonAsync($"{api}/interviews/{interview.Id()}/notified",
            new { status = "NotifiedManually", expectedVersion = interview.Int("version") })).Json(HttpStatusCode.OK);
        interview = await (await client.PostAsJsonAsync($"{api}/interviews/{interview.Id()}/finish",
            new { outcome = "Completed", expectedVersion = interview.Int("version") })).Json(HttpStatusCode.OK);
        Assert.Equal("Interviewing", (await client.GetJson($"/api/v1/staffing/deals/{dealId}")).Str("stage"));

        await (await client.PostAsJsonAsync($"{api}/feedback", new
        {
            submissionId = submission.Id(), interviewId = interview.Id(), source = "Client", decision = "Selected",
            detail = "Strong Azure answers", sharedWithCandidate = true
        })).Json(HttpStatusCode.OK);
        var internalShared = await client.PostAsJsonAsync($"{api}/feedback", new
        {
            submissionId = submission.Id(), source = "Internal", decision = "None", detail = "Margin note", sharedWithCandidate = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, internalShared.StatusCode);

        var offer = await (await client.PostAsJsonAsync($"{api}/offers", new
        {
            submissionId = submission.Id(), clientRate = 60, candidatePay = 45, currency = "USD", unit = "Hour", startDate = "2026-11-02", placementValue = 24000
        })).Json(HttpStatusCode.OK);
        offer = await (await client.PostAsJsonAsync($"{api}/offers/{offer.Id()}/state", new { state = "Extended", expectedVersion = offer.Int("version") })).Json(HttpStatusCode.OK);
        offer = await (await client.PostAsJsonAsync($"{api}/offers/{offer.Id()}/state", new { state = "Accepted", expectedVersion = offer.Int("version") })).Json(HttpStatusCode.OK);
        var unsigned = await client.PostAsJsonAsync($"{api}/offers/{offer.Id()}/outcome", new { outcome = "Placed", expectedVersion = offer.Int("version") });
        Assert.Equal(HttpStatusCode.Conflict, unsigned.StatusCode);
        offer = await (await client.PostAsJsonAsync($"{api}/offers/{offer.Id()}/contract", new
        {
            status = "Signed", contractVersion = "MSA-2 / SOW-7", signatureProvider = "DocuSign", signedDocumentReference = "envelope 7f3a", expectedVersion = offer.Int("version")
        })).Json(HttpStatusCode.OK);
        Assert.DoesNotContain("envelope 7f3a", offer.GetRawText());
        await (await client.PostAsJsonAsync($"{api}/offers/{offer.Id()}/outcome", new { outcome = "Placed", expectedVersion = offer.Int("version") })).Json(HttpStatusCode.OK);

        var deal = await client.GetJson($"/api/v1/staffing/deals/{dealId}");
        Assert.Equal("Won", deal.Str("stage"));
        var stages = deal.GetProperty("activities").EnumerateArray().Where(a => a.Str("type") == "StageChanged").Select(a => a.Str("detail")).ToList();
        Assert.Contains("Interviewing → Offer", stages);
        Assert.Contains("Contracting → Won", stages);

        var kpis = await client.GetJson("/api/v1/staffing/kpis");
        Assert.Equal(1, kpis.Int("wonDeals"));
        Assert.Equal(1, kpis.Int("placements"));
        Assert.Equal(1, kpis.Int("contractsSigned"));
        Assert.Equal(1, kpis.Int("submissionsSent"));
        var placement = kpis.GetProperty("placementValue").EnumerateArray().Single();
        Assert.Equal(24000m, placement.GetProperty("amount").GetDecimal());
        var replied = kpis.GetProperty("conversions").EnumerateArray().Single(c => c.Str("from") == "Contacted");
        Assert.Equal(1.0, replied.GetProperty("rate").GetDouble());

        var work = await client.GetJson($"{api}/work");
        Assert.Single(work.GetProperty("submissions").EnumerateArray());
        Assert.Single(work.GetProperty("interviews").EnumerateArray());
        Assert.Single(work.GetProperty("feedback").EnumerateArray());
    }

    [Fact]
    public async Task Consent_and_approval_guards_hold()
    {
        var (client, dealId) = await DealAtRequirementConfirmedAsync(factory, "staffing-guards@example.test");
        var api = $"/api/v1/staffing/deals/{dealId}";

        var noConsent = await (await client.PostAsJsonAsync("/api/v1/staffing/candidates", new { name = "No Consent", availability = "Unknown" })).Json(HttpStatusCode.Created);
        var refused = await client.PostAsJsonAsync($"{api}/submissions", new { candidateId = noConsent.Id(), sharedFields = new[] { "Name" } });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("consent", await refused.Content.ReadAsStringAsync());

        var candidate = await CandidateWithConsentAsync(client, ["Name", "Skills"]);
        var tooMuch = await client.PostAsJsonAsync($"{api}/submissions", new { candidateId = candidate.Id(), sharedFields = new[] { "Name", "Email" } });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Contains("Email", await tooMuch.Content.ReadAsStringAsync());

        var submission = await (await client.PostAsJsonAsync($"{api}/submissions", new { candidateId = candidate.Id(), sharedFields = new[] { "Name" } })).Json(HttpStatusCode.OK);
        var notApproved = await client.PostAsJsonAsync($"{api}/submissions/{submission.Id()}/sent",
            new { expectedVersion = submission.Int("version"), channel = "Email", receipt = "x" });
        Assert.Equal(HttpStatusCode.Conflict, notApproved.StatusCode);

        // The candidate's details change after the draft: approval is refused until the snapshot is refreshed.
        var current = await client.GetJson($"/api/v1/staffing/candidates/{candidate.Id()}");
        await (await client.PutAsJsonAsync($"/api/v1/staffing/candidates/{candidate.Id()}", new
        {
            name = "Priya S.", availability = "Immediate", rateAmount = 50, rateCurrency = "USD", rateUnit = "Hour", notifyByEmail = false,
            expectedVersion = current.Int("version")
        })).Json(HttpStatusCode.OK);
        var edited = await client.GetJson($"/api/v1/staffing/candidates/{candidate.Id()}");
        Assert.True(edited.GetProperty("hasResume").GetBoolean()); // an edit without resume text keeps the stored resume
        Assert.Equal(1, edited.Int("resumeVersion"));
        var stale = await client.PostAsJsonAsync($"{api}/submissions/{submission.Id()}/approve", new { expectedVersion = submission.Int("version") });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var refreshed = await (await client.PutAsJsonAsync($"{api}/submissions/{submission.Id()}",
            new { candidateId = candidate.Id(), sharedFields = new[] { "Name" }, expectedVersion = submission.Int("version") })).Json(HttpStatusCode.OK);
        Assert.Equal("Priya S.", refreshed.GetProperty("snapshot").Str("name"));
        await (await client.PostAsJsonAsync($"{api}/submissions/{submission.Id()}/approve", new { expectedVersion = refreshed.Int("version") })).Json(HttpStatusCode.OK);

        // Another owner cannot see this deal's work.
        var other = PostgresApiFactory.ClientFor(factory, "staffing-other@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{api}/work")).StatusCode);
    }

    [Fact]
    public async Task Proposals_quote_only_from_an_active_rate_card_and_edits_clear_approval()
    {
        var (client, dealId) = await DealAtRequirementConfirmedAsync(factory, "staffing-proposals@example.test");
        var card = await (await client.PostAsJsonAsync("/api/v1/staffing/rate-cards", new
        {
            name = "2026 India delivery", currency = "usd",
            lines = new[] { new { role = ".NET engineer", seniority = "Senior", unit = "Hour", rate = 40m }, new { role = "QA engineer", seniority = (string?)null, unit = "Hour", rate = 25m } },
            terms = "Net 30"
        })).Json(HttpStatusCode.OK);
        Assert.Equal("USD", card.Str("currency"));
        var draftCard = await client.PostAsJsonAsync($"/api/v1/staffing/deals/{dealId}/proposals", new { rateCardId = card.Id(), title = "Q" });
        Assert.Equal(HttpStatusCode.Conflict, draftCard.StatusCode);
        card = await (await client.PostAsJsonAsync($"/api/v1/staffing/rate-cards/{card.Id()}/status", new { status = "Active", expectedVersion = card.Int("version") })).Json(HttpStatusCode.OK);

        var above = await client.PostAsJsonAsync($"/api/v1/staffing/deals/{dealId}/proposals", new
        {
            rateCardId = card.Id(), title = "Q", lines = new[] { new { role = ".NET engineer", seniority = "Senior", unit = "Hour", rate = 55m } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, above.StatusCode);

        var proposal = await (await client.PostAsJsonAsync($"/api/v1/staffing/deals/{dealId}/proposals", new
        {
            rateCardId = card.Id(), title = "Acme team", body = "Dear [client name], we propose…",
            lines = new[] { new { role = ".NET engineer", seniority = "Senior", unit = "Hour", rate = 38m } }
        })).Json(HttpStatusCode.OK);
        Assert.Equal("Net 30", proposal.Str("terms"));
        var placeholder = await client.PostAsJsonAsync($"/api/v1/staffing/deals/{dealId}/proposals/{proposal.Id()}/approve", new { expectedVersion = proposal.Int("version") });
        Assert.Equal(HttpStatusCode.Conflict, placeholder.StatusCode);

        proposal = await (await client.PutAsJsonAsync($"/api/v1/staffing/deals/{dealId}/proposals/{proposal.Id()}", new
        {
            title = "Acme team", body = "Dear Ms Rao, we propose…", terms = "Net 30",
            lines = new[] { new { role = ".NET engineer", seniority = "Senior", unit = "Hour", rate = 38m } }, expectedVersion = proposal.Int("version")
        })).Json(HttpStatusCode.OK);
        proposal = await (await client.PostAsJsonAsync($"/api/v1/staffing/deals/{dealId}/proposals/{proposal.Id()}/approve", new { expectedVersion = proposal.Int("version") })).Json(HttpStatusCode.OK);
        Assert.Equal("Approved", proposal.Str("state"));
        proposal = await (await client.PutAsJsonAsync($"/api/v1/staffing/deals/{dealId}/proposals/{proposal.Id()}", new
        {
            title = "Acme team v2", body = "Dear Ms Rao, we propose…", terms = "Net 30",
            lines = new[] { new { role = ".NET engineer", seniority = "Senior", unit = "Hour", rate = 38m } }, expectedVersion = proposal.Int("version")
        })).Json(HttpStatusCode.OK);
        Assert.Equal("Draft", proposal.Str("state"));
    }
}
