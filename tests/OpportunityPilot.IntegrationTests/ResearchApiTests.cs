using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpportunityPilot.Infrastructure.Research;

namespace OpportunityPilot.IntegrationTests;

/// <summary>Campaigns → sources → research → opportunities against real PostgreSQL, with the runner driven by the test.</summary>
public class ResearchApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly object JobCriteria = new
    {
        keywords = new[] { ".NET developer" },
        requiredSkills = new[] { "C#", ".NET" },
        preferredSkills = new[] { "Docker" },
        candidateYears = 4,
        locations = new[] { "Pune", "Remote" }
    };

    // Fictional postings: one matches everything in a listed location, one has none of the required skills,
    // one does not say where it is.
    private const string ThreePostings = """
        Senior .NET Engineer
        Company: Acme
        Location: Pune
        URL: https://jobs.example/acme-1
        We build APIs in C# on ASP.NET Core and ship them with Docker. 3-5 years of experience.
        ---
        Java Developer
        Company: Globex
        Location: Pune
        Spring Boot and Kafka. 5+ years.
        ---
        Backend Engineer
        Company: Initech
        We use C# and .NET with Docker. Minimum 4 years.
        """;

    private static JsonElement ByTitle(IEnumerable<JsonElement> items, string title) => items.Single(i => i.Str("title") == title);

    [Fact]
    public async Task Job_research_over_pasted_postings_produces_traceable_outcomes_and_scores()
    {
        var user = factory.ClientFor("research-job@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", JobCriteria, "Backend roles");
        Assert.Equal(40, campaign.GetProperty("weights").GetProperty("mandatorySkills").GetInt32());
        Assert.Equal(25, campaign.Int("resultLimit"));
        var source = await ResearchApi.AddPasteAsync(user, campaign.Id(), ThreePostings);
        Assert.Equal(3, source.Int("itemCount"));
        Assert.Equal("Pending", source.Str("status"));

        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        Assert.Equal("Queued", (await ResearchApi.JobAsync(user, jobId)).Str("state"));
        Assert.Equal(jobId, await ResearchApi.QueueAsync(user, campaign.Id()));   // no duplicate run while queued

        Assert.True(await factory.RunResearchAsync() >= 1);

        var job = await ResearchApi.JobAsync(user, jobId);
        Assert.Equal("Completed", job.Str("state"));
        Assert.Equal("Complete", job.Str("stage"));
        var counts = job.GetProperty("counts");
        Assert.Equal(1, counts.Int("sources"));
        Assert.Equal(1, counts.Int("sourcesDone"));
        Assert.Equal(3, counts.Int("candidates"));
        Assert.Equal(1, counts.Int("qualified"));
        Assert.Equal(1, counts.Int("needsVerification"));
        Assert.Equal(1, counts.Int("excluded"));
        var events = job.GetProperty("events").EnumerateArray().ToList();
        Assert.Equal("Complete", events[0].Str("stage"));                    // newest first
        Assert.Contains(events, e => e.Str("stage") == "Prepare" && e.Str("message").Contains("2 required skills"));
        Assert.DoesNotContain(events, e => e.Str("message").Contains("Exception"));

        var items = await ResearchApi.OpportunitiesAsync(user, campaign.Id());
        Assert.Equal([100, 80, 30], items.Select(i => i.Int("score")));      // sorted by score

        var qualified = ByTitle(items, "Senior .NET Engineer");
        Assert.Equal("Qualified", qualified.Str("outcome"));
        Assert.Equal(100, qualified.Int("coverage"));
        Assert.Equal("Acme", qualified.Str("organization"));
        Assert.Equal("Pune", qualified.Str("location"));
        Assert.Equal("New", qualified.Str("status"));
        Assert.Equal("https://jobs.example/acme-1", qualified.Str("applyUrl"));

        var excluded = ByTitle(items, "Java Developer");
        Assert.Equal("Excluded", excluded.Str("outcome"));
        Assert.Contains("None of the required skills", excluded.Str("outcomeReason"));
        Assert.Equal(100, excluded.Int("coverage"));

        var unverified = ByTitle(items, "Backend Engineer");
        Assert.Equal("NeedsVerification", unverified.Str("outcome"));
        Assert.Equal(80, unverified.Int("coverage"));                        // location unknown: 20 of 100 not covered
        Assert.True(unverified.Int("gapsCount") >= 1);

        var detail = await user.GetJson($"/api/v1/opportunities/{qualified.Id()}");
        var evidence = Assert.Single(detail.GetProperty("evidence").EnumerateArray().ToList());
        Assert.Contains("Senior .NET Engineer", evidence.Str("excerpt"));
        Assert.Equal("Pasted text", evidence.Str("sourceLabel"));
        Assert.Equal("Rules", evidence.Str("extractionMethod"));
        Assert.Equal("https://jobs.example/acme-1", evidence.Str("url"));
        var breakdown = detail.GetProperty("breakdown").EnumerateArray().ToList();
        Assert.Equal(["mandatorySkills", "preferredSkills", "experience", "location"], breakdown.Select(b => b.Str("criterion")));
        Assert.All(breakdown, b =>
        {
            Assert.False(string.IsNullOrWhiteSpace(b.Str("reason")));
            Assert.Equal(evidence.Id().ToString(), Assert.Single(b.GetProperty("evidenceIds").EnumerateArray().ToList()).GetString());
        });
        Assert.Equal(100, breakdown.Sum(b => b.GetProperty("points").GetDouble()), 1);
        Assert.Contains(detail.GetProperty("facts").EnumerateArray(), f => f.Str("key") == "experience" && f.Str("evidenceId") == evidence.Id().ToString());
        Assert.Contains(detail.GetProperty("activities").EnumerateArray(), a => a.Str("kind") == "Researched");

        var unverifiedDetail = await user.GetJson($"/api/v1/opportunities/{unverified.Id()}");
        var location = unverifiedDetail.GetProperty("breakdown").EnumerateArray().Single(b => b.Str("criterion") == "location");
        Assert.Equal(JsonValueKind.Null, location.GetProperty("value").ValueKind);
        Assert.Empty(location.GetProperty("evidenceIds").EnumerateArray());
        Assert.Contains("Location not stated", unverifiedDetail.GetProperty("gaps").EnumerateArray().Select(g => g.GetString()));

        var filtered = await ResearchApi.OpportunitiesAsync(user, campaign.Id(), "?outcome=Excluded");
        Assert.Equal("Java Developer", Assert.Single(filtered).Str("title"));

        var sources = await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/sources");
        Assert.Equal("Ok", sources[0].Str("status"));

        var summary = (await user.GetJson("/api/v1/campaigns"))[0];
        Assert.Equal(3, summary.Int("opportunityCount"));
        Assert.Equal(1, summary.Int("sourceCount"));
        Assert.Equal("Completed", summary.GetProperty("lastJob").Str("state"));
    }

    [Fact]
    public async Task Rerunning_research_updates_in_place_and_keeps_the_users_status()
    {
        var user = factory.ClientFor("research-rerun@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", JobCriteria);
        await ResearchApi.AddPasteAsync(user, campaign.Id(), ThreePostings);
        await ResearchApi.QueueAsync(user, campaign.Id());
        await factory.RunResearchAsync();

        var first = await ResearchApi.OpportunitiesAsync(user, campaign.Id());
        var target = ByTitle(first, "Backend Engineer");
        var patched = await (await user.PatchAsJsonAsync($"/api/v1/opportunities/{target.Id()}/status", new { status = "Shortlisted" }))
            .Json(HttpStatusCode.OK);
        Assert.Equal("Shortlisted", patched.Str("status"));
        var evidenceBefore = patched.GetProperty("evidence")[0].Id();

        // Same sources again, plus one new posting.
        await ResearchApi.AddPasteAsync(user, campaign.Id(), "Platform Engineer\nCompany: Hooli\nLocation: Remote\nC# and .NET, 3 to 5 yrs.");
        var second = await ResearchApi.QueueAsync(user, campaign.Id());
        await factory.RunResearchAsync();
        Assert.Equal("Completed", (await ResearchApi.JobAsync(user, second)).Str("state"));

        var after = await ResearchApi.OpportunitiesAsync(user, campaign.Id());
        Assert.Equal(4, after.Count);
        Assert.Equal(first.Select(i => i.Id()).Order(), after.Where(i => i.Str("title") != "Platform Engineer").Select(i => i.Id()).Order());
        var kept = await user.GetJson($"/api/v1/opportunities/{target.Id()}");
        Assert.Equal("Shortlisted", kept.Str("status"));
        Assert.Equal(evidenceBefore, kept.GetProperty("evidence")[0].Id());   // identical excerpt → same evidence row
        Assert.Contains(kept.GetProperty("activities").EnumerateArray(), a => a.Str("kind") == "StatusChanged" && a.Str("detail") == "New → Shortlisted");
        Assert.Single(await ResearchApi.OpportunitiesAsync(user, campaign.Id(), "?status=Shortlisted"));

        var overview = await user.GetJson("/api/v1/overview");
        Assert.Equal(1, overview.Int("shortlisted"));
        Assert.Equal(1, overview.Int("campaigns"));
    }

    [Fact]
    public async Task Csv_preview_reports_row_errors_and_commit_creates_a_source_that_research_reads()
    {
        var user = factory.ClientFor("research-csv@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", JobCriteria);

        var missingColumn = await (await user.PostAsJsonAsync("/api/v1/imports/preview",
            new { campaignId = campaign.Id(), csv = "title,location\nDev,Pune\nQA,Remote\n" })).Json(HttpStatusCode.OK);
        Assert.Contains(missingColumn.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("\"company\" is missing"));
        Assert.Equal(0, missingColumn.Int("validCount"));
        Assert.All(missingColumn.GetProperty("rows").EnumerateArray(),
            r => Assert.Contains(r.GetProperty("errors").EnumerateArray(), e => e.GetString()!.Contains("company is required")));

        const string csv = "Title,Company,Location,URL,Description,Extra\r\n" +
                           "Platform Engineer,Acme,Pune,https://jobs.example/p1,\"C#, .NET and Docker; 3-5 years\",x\r\n" +
                           "Data Engineer,,Pune,,Python,y\r\n" +
                           "\"QA, Senior\",Initech,Remote,ftp://bad.example,C# tests,z\r\n";
        var preview = await (await user.PostAsJsonAsync("/api/v1/imports/preview", new { campaignId = campaign.Id(), csv })).Json(HttpStatusCode.OK);
        Assert.Equal(["Title", "Company", "Location", "URL", "Description", "Extra"], preview.GetProperty("columns").EnumerateArray().Select(c => c.GetString()));
        Assert.Contains(preview.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("\"Extra\""));
        Assert.Equal(1, preview.Int("validCount"));
        Assert.Equal(2, preview.Int("errorCount"));
        var rows = preview.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal([2, 3, 4], rows.Select(r => r.Int("row")));
        Assert.Equal("C#, .NET and Docker; 3-5 years", rows[0].GetProperty("values").Str("description"));
        Assert.Contains("company is required.", rows[1].GetProperty("errors").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains(rows[2].GetProperty("errors").EnumerateArray(), e => e.GetString()!.Contains("url"));

        var importId = preview.GetProperty("importId").GetGuid();
        var source = await (await user.PostAsJsonAsync($"/api/v1/imports/{importId}/commit", new { label = "Jobs export" })).Json(HttpStatusCode.Created);
        Assert.Equal("Csv", source.Str("kind"));
        Assert.Equal("Jobs export", source.Str("label"));
        Assert.Equal(1, source.Int("itemCount"));
        Assert.Equal(HttpStatusCode.Conflict, (await user.PostAsJsonAsync($"/api/v1/imports/{importId}/commit", new { })).StatusCode);

        var invalid = await user.PostAsJsonAsync("/api/v1/imports/preview", new { campaignId = campaign.Id(), csv = "title,company\n\"unterminated,x\n" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        await ResearchApi.QueueAsync(user, campaign.Id());
        await factory.RunResearchAsync();
        var item = Assert.Single(await ResearchApi.OpportunitiesAsync(user, campaign.Id()));
        Assert.Equal("Platform Engineer", item.Str("title"));
        Assert.Equal("Qualified", item.Str("outcome"));
        Assert.Equal("https://jobs.example/p1", item.Str("url"));

        // Committing the same file again does not multiply opportunities.
        var again = await (await user.PostAsJsonAsync("/api/v1/imports/preview", new { campaignId = campaign.Id(), csv })).Json(HttpStatusCode.OK);
        await user.PostAsJsonAsync($"/api/v1/imports/{again.GetProperty("importId").GetGuid()}/commit", new { });
        await ResearchApi.QueueAsync(user, campaign.Id());
        await factory.RunResearchAsync();
        Assert.Single(await ResearchApi.OpportunitiesAsync(user, campaign.Id()));
    }

    [Fact]
    public async Task Customer_research_scores_industry_problem_geography_signal_and_contact_path()
    {
        var user = factory.ClientFor("research-customer@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Customer", new
        {
            industries = new[] { "fintech" },
            problems = new[] { "reconciliation", "invoicing" },
            locations = new[] { "India" },
            signals = new[] { "hiring", "funding" }
        });
        Assert.Equal(30, campaign.GetProperty("weights").GetProperty("problem").GetInt32());
        await ResearchApi.AddPasteAsync(user, campaign.Id(), """
            Ledgerly
            Website: ledgerly.example
            Country: India
            Industry: Fintech
            Ledgerly automates invoicing for small businesses.
            ---
            Shoebox
            Country: Germany
            We sell shoes online. We are hiring.
            ---
            Mystery Co
            A company.
            """);
        await ResearchApi.QueueAsync(user, campaign.Id());
        await factory.RunResearchAsync();

        var items = await ResearchApi.OpportunitiesAsync(user, campaign.Id());
        var ledgerly = ByTitle(items, "Ledgerly");
        Assert.Equal(65, ledgerly.Int("score"));            // the plan's worked example: 25 + 15 + 15 + 0 + 10
        Assert.Equal(80, ledgerly.Int("coverage"));
        Assert.Equal("Qualified", ledgerly.Str("outcome"));
        Assert.Equal("https://ledgerly.example", ledgerly.Str("url"));

        var detail = await user.GetJson($"/api/v1/opportunities/{ledgerly.Id()}");
        var values = detail.GetProperty("breakdown").EnumerateArray().ToDictionary(b => b.Str("criterion"), b => b.GetProperty("value"));
        Assert.Equal(1, values["industry"].GetDouble());
        Assert.Equal(0.5, values["problem"].GetDouble());
        Assert.Equal(1, values["geography"].GetDouble());
        Assert.Equal(JsonValueKind.Null, values["signal"].ValueKind);
        Assert.Equal(1, values["contactPath"].GetDouble());

        var shoebox = ByTitle(items, "Shoebox");
        Assert.Equal("Excluded", shoebox.Str("outcome"));
        Assert.Equal(20, shoebox.Int("score"));             // only the hiring signal
        Assert.Equal(90, shoebox.Int("coverage"));          // contact path unknown

        var mystery = ByTitle(items, "Mystery Co");
        Assert.Equal("NeedsVerification", mystery.Str("outcome"));
        Assert.Equal(0, mystery.Int("score"));
        Assert.Equal(55, mystery.Int("coverage"));

        var csv = await user.GetAsync($"/api/v1/campaigns/{campaign.Id()}/export");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var text = await csv.Content.ReadAsStringAsync();
        Assert.StartsWith("\"Title\",\"Organization\"", text.TrimStart('\uFEFF'));
        Assert.Contains("\"Ledgerly\"", text);
    }

    [Fact]
    public async Task Export_neutralises_spreadsheet_formulas()
    {
        var user = factory.ClientFor("research-export@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Customer", new { industries = new[] { "fintech" } }, "=cmd|' /C calc'!A0");
        await ResearchApi.AddPasteAsync(user, campaign.Id(), "=HYPERLINK(\"http://evil.example\",\"click\")\nIndustry: fintech");
        await ResearchApi.QueueAsync(user, campaign.Id());
        await factory.RunResearchAsync();

        var response = await user.GetAsync($"/api/v1/campaigns/{campaign.Id()}/export");
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil.example\"\",\"\"click\"\")\"", text);
        Assert.DoesNotContain(",\"=", text);
        Assert.DoesNotContain("\n\"=", text);
        var fileName = response.Content.Headers.ContentDisposition?.FileName ?? "";
        Assert.EndsWith("-opportunities.csv", fileName.Trim('"'));
        Assert.DoesNotContain("=", fileName);
        Assert.DoesNotContain("|", fileName);
    }

    [Fact]
    public async Task Other_users_get_404_for_campaigns_jobs_opportunities_and_sources()
    {
        var alice = factory.ClientFor("research-alice@example.test");
        var bob = factory.ClientFor("research-bob@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(alice, "Job", JobCriteria);
        var source = await ResearchApi.AddPasteAsync(alice, campaign.Id(), ThreePostings);
        var jobId = await ResearchApi.QueueAsync(alice, campaign.Id());
        await factory.RunResearchAsync();
        var opportunity = (await ResearchApi.OpportunitiesAsync(alice, campaign.Id()))[0];
        var c = campaign.Id();

        foreach (var url in new[]
                 {
                     $"/api/v1/campaigns/{c}", $"/api/v1/campaigns/{c}/sources", $"/api/v1/campaigns/{c}/research-jobs",
                     $"/api/v1/research-jobs/{jobId}", $"/api/v1/campaigns/{c}/opportunities", $"/api/v1/opportunities/{opportunity.Id()}",
                     $"/api/v1/campaigns/{c}/export"
                 })
            Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(url)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync($"/api/v1/campaigns/{c}/sources", new { kind = "Paste", text = "Hijack\nCompany: X" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/campaigns/{c}/sources/{source.Id()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/v1/campaigns/{c}/research", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/v1/research-jobs/{jobId}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PatchAsJsonAsync($"/api/v1/opportunities/{opportunity.Id()}/status", new { status = "Dismissed" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/v1/campaigns/{c}",
            new { name = "Hijacked", goal = "", criteria = new { }, weights = (object?)null, resultLimit = 10, expectedVersion = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/v1/imports/preview", new { campaignId = c, csv = "title,company\nA,B" })).StatusCode);

        // Bob cannot point a campaign at Alice's profile either.
        var bobCampaign = await bob.PostAsJsonAsync("/api/v1/campaigns",
            new { profileId = campaign.GetProperty("profileId").GetGuid(), mode = "Job", name = "Mine", goal = "", criteria = new { } });
        Assert.Equal(HttpStatusCode.BadRequest, bobCampaign.StatusCode);
        Assert.Contains("profileId", await bobCampaign.Content.ReadAsStringAsync());

        Assert.Empty((await bob.GetJson("/api/v1/campaigns")).EnumerateArray());
        Assert.Equal("New", (await alice.GetJson($"/api/v1/opportunities/{opportunity.Id()}")).Str("status"));
        Assert.Single((await alice.GetJson($"/api/v1/campaigns/{c}/sources")).EnumerateArray());
    }

    [Fact]
    public async Task Stale_campaign_edit_is_rejected_with_409_and_unsupported_modes_with_400()
    {
        var user = factory.ClientFor("research-versions@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", JobCriteria);
        Assert.Equal(1, campaign.Int("version"));

        var edit = new { name = "Renamed", goal = "Better goal", criteria = JobCriteria, weights = new { mandatorySkills = 50, experience = 50 }, resultLimit = 10, expectedVersion = 1 };
        var updated = await (await user.PutAsJsonAsync($"/api/v1/campaigns/{campaign.Id()}", edit)).Json(HttpStatusCode.OK);
        Assert.Equal(2, updated.Int("version"));
        Assert.Equal(50, updated.GetProperty("weights").GetProperty("experience").GetInt32());
        Assert.Equal(0, updated.GetProperty("weights").GetProperty("location").GetInt32());

        var stale = await user.PutAsJsonAsync($"/api/v1/campaigns/{campaign.Id()}", edit);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("correlationId", await stale.Content.ReadAsStringAsync());

        var profileId = campaign.GetProperty("profileId").GetGuid();
        // Every OpportunityMode is supported since API #16; a mode outside the enum is still rejected.
        var unknownMode = await user.PostAsJsonAsync("/api/v1/campaigns", new { profileId, mode = "Lottery", name = "P", goal = "", criteria = new { } });
        Assert.Equal(HttpStatusCode.BadRequest, unknownMode.StatusCode);

        var badWeights = await user.PostAsJsonAsync("/api/v1/campaigns",
            new { profileId, mode = "Job", name = "W", goal = "", criteria = new { }, weights = new { industry = 50 } });
        Assert.Equal(HttpStatusCode.BadRequest, badWeights.StatusCode);
    }

    [Fact]
    public async Task Cancelling_a_queued_job_stops_it_before_anything_is_written()
    {
        var user = factory.ClientFor("research-cancel@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", JobCriteria);
        await ResearchApi.AddPasteAsync(user, campaign.Id(), ThreePostings);
        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());

        var cancelled = await (await user.PostAsync($"/api/v1/research-jobs/{jobId}/cancel", null)).Json(HttpStatusCode.Accepted);
        Assert.Equal("Cancelled", cancelled.Str("state"));
        Assert.NotEqual(JsonValueKind.Null, cancelled.GetProperty("finishedAt").ValueKind);

        await factory.RunResearchAsync();

        Assert.Equal("Cancelled", (await ResearchApi.JobAsync(user, jobId)).Str("state"));
        Assert.Empty(await ResearchApi.OpportunitiesAsync(user, campaign.Id()));
        var jobs = (await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/research-jobs")).EnumerateArray().ToList();
        Assert.False(jobs[0].TryGetProperty("events", out _));                // lists omit events
        Assert.NotEqual(jobId, await ResearchApi.QueueAsync(user, campaign.Id())); // a new run can be queued afterwards
        await factory.RunResearchAsync();
    }

    [Fact]
    public async Task Research_needs_a_source_and_sources_validate_their_input()
    {
        var user = factory.ClientFor("research-validation@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", JobCriteria);

        Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsync($"/api/v1/campaigns/{campaign.Id()}/research", null)).StatusCode);

        async Task<string> Rejected(object body)
        {
            var response = await user.PostAsJsonAsync($"/api/v1/campaigns/{campaign.Id()}/sources", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }

        Assert.Contains("text", await Rejected(new { kind = "Paste", text = "" }));
        Assert.Contains("50,000", await Rejected(new { kind = "Paste", text = new string('x', 50_001) }));
        Assert.Contains("url", await Rejected(new { kind = "Url", url = "ftp://example.com/x" }));
        Assert.Contains("url", await Rejected(new { kind = "Feed", url = "https://user:secret@example.com/feed" }));
        Assert.Contains("kind", await Rejected(new { kind = "Csv" }));
        Assert.Contains("kind", await Rejected(new { kind = "Agent" }));

        var source = await ResearchApi.AddPasteAsync(user, campaign.Id(), "Dev\nCompany: Acme");
        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync($"/api/v1/campaigns/{campaign.Id()}/sources/{source.Id()}")).StatusCode);
        Assert.Empty((await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/sources")).EnumerateArray());
    }

    [Fact]
    public async Task Blocked_addresses_fail_the_source_safely_and_the_run_completes_with_gaps()
    {
        // The classifier the fetcher relies on, for exactly the addresses used below.
        Assert.False(AddressClassifier.IsPublic(System.Net.IPAddress.Parse("127.0.0.1")));
        Assert.False(AddressClassifier.IsPublic(System.Net.IPAddress.Parse("169.254.169.254")));
        Assert.False(AddressClassifier.IsPublic(System.Net.IPAddress.Parse("::ffff:127.0.0.1")));

        var user = factory.ClientFor("research-ssrf@example.test");
        var campaign = await ResearchApi.CreateCampaignAsync(user, "Job", JobCriteria);
        await ResearchApi.AddPasteAsync(user, campaign.Id(), ThreePostings);
        await ResearchApi.AddUrlAsync(user, campaign.Id(), "Url", "https://127.0.0.1/");
        await ResearchApi.AddUrlAsync(user, campaign.Id(), "Url", "http://169.254.169.254/latest/meta-data/");   // http: Development only
        await ResearchApi.AddUrlAsync(user, campaign.Id(), "Feed", "https://localhost/feed.xml");
        await ResearchApi.AddUrlAsync(user, campaign.Id(), "Url", "https://[::ffff:127.0.0.1]/");

        var jobId = await ResearchApi.QueueAsync(user, campaign.Id());
        await factory.RunResearchAsync();

        var job = await ResearchApi.JobAsync(user, jobId);
        Assert.Equal("CompletedWithGaps", job.Str("state"));
        Assert.Contains("4 sources could not be read", job.Str("safeError"));
        Assert.Equal(4, job.GetProperty("counts").Int("sourcesFailed"));
        Assert.Equal(3, job.GetProperty("counts").Int("candidates"));      // the pasted source still produced results

        var failed = (await user.GetJson($"/api/v1/campaigns/{campaign.Id()}/sources")).EnumerateArray()
            .Where(s => s.Str("kind") is "Url" or "Feed").ToList();
        Assert.Equal(4, failed.Count);
        Assert.All(failed, s =>
        {
            Assert.Equal("Failed", s.Str("status"));
            Assert.Equal("The address is private, local or reserved, so it is never fetched.", s.Str("safeError"));
        });
        Assert.Equal(3, (await ResearchApi.OpportunitiesAsync(user, campaign.Id())).Count);
    }
}
