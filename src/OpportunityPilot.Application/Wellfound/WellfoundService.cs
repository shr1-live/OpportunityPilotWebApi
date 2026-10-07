using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Domain.Wellfound;

namespace OpportunityPilot.Application.Wellfound;

public sealed class WellfoundService(IAppDbContext db, ICurrentUser user, TimeProvider clock, IWellfoundPublicJobReader publicReader)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WellfoundStatusDto Status() => new(
        "PublicReady", false, false, "https://wellfound.com/api/mcp", "https://reach.wellfound.com/mcp",
        ["applications:read"], ["projects:read", "agents:read", "candidates:read", "company_lists:read"],
        "Public job discovery is available without an account. Recruiter-owned jobs and applicants still require Wellfound Recruit/Reach OAuth.");

    public async Task<SyncWellfoundPublicResult> SyncPublicAsync(CancellationToken ct)
    {
        var result = await publicReader.ReadAsync(ct);
        if (!result.Ok) throw new ConflictException(result.FailureReason ?? "Wellfound public job import failed.");

        var demoJobs = await db.WellfoundJobs.Where(x => x.OwnerId == user.OwnerId && x.IsDemo).ToListAsync(ct);
        var demoJobIds = demoJobs.Select(x => x.Id).ToArray();
        var demoApps = await db.WellfoundApplications.Where(x => x.OwnerId == user.OwnerId && x.IsDemo).ToListAsync(ct);
        var demoAppIds = demoApps.Select(x => x.Id).ToArray();
        var demoActivities = await db.WellfoundActivities.Where(x => x.OwnerId == user.OwnerId &&
            ((x.JobId != null && demoJobIds.Contains(x.JobId.Value)) ||
             (x.ApplicationId != null && demoAppIds.Contains(x.ApplicationId.Value)) ||
             x.Detail.Contains("labelled Wellfound demo"))).ToListAsync(ct);
        db.WellfoundActivities.RemoveRange(demoActivities);
        db.WellfoundApplications.RemoveRange(demoApps);
        db.WellfoundJobs.RemoveRange(demoJobs);

        var providerIds = result.Jobs.Select(x => x.ProviderJobId).ToArray();
        var existing = await db.WellfoundJobs.Where(x => x.OwnerId == user.OwnerId && !x.IsDemo &&
            x.Scope == WellfoundJobScope.CandidateDiscovery && providerIds.Contains(x.ProviderJobId))
            .ToDictionaryAsync(x => x.ProviderJobId, StringComparer.OrdinalIgnoreCase, ct);
        var added = 0;
        var updated = 0;
        foreach (var source in result.Jobs)
        {
            if (!existing.TryGetValue(source.ProviderJobId, out var job))
            {
                job = new WellfoundJob(user.OwnerId, source.ProviderJobId, WellfoundJobScope.CandidateDiscovery,
                    source.Title, source.CompanyName, source.ApplyUrl, false, result.ObservedAt);
                db.WellfoundJobs.Add(job);
                added++;
            }
            else updated++;
            job.RefreshPublic(source.Title, source.CompanyName, source.ApplyUrl, source.Location, source.RemoteType,
                source.SalaryMin, source.SalaryMax, source.Currency, source.EquityMin, source.EquityMax,
                source.PostedAt, source.EvidenceJson, result.ObservedAt);
        }

        db.WellfoundActivities.Add(new(user.OwnerId, WellfoundActivityKind.SyncObserved,
            $"Observed {result.Jobs.Count} current public Wellfound job cards; {added} added and {updated} refreshed. No authenticated account data was used.",
            result.ObservedAt));
        await db.SaveChangesAsync(ct);
        return new(result.Jobs.Count, added, updated, demoJobs.Count + demoApps.Count, result.ObservedAt);
    }

    public async Task<IReadOnlyList<WellfoundJobDto>> JobsAsync(string workspace, string? keyword, string? company,
        string? location, string? techStack, string? workMode, decimal? minSalary, bool equityOnly,
        string? fundingStage, string? industry, string? employmentType, int? postedWithinDays,
        WellfoundJobState? state, string sort, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 200);
        var isSales = workspace.Equals("Sales", StringComparison.OrdinalIgnoreCase);
        var query = db.WellfoundJobs.Where(x => x.OwnerId == user.OwnerId &&
            (isSales || x.Scope == WellfoundJobScope.CandidateDiscovery));
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.Trim();
            query = query.Where(x => x.Title.Contains(value) || x.CompanyName.Contains(value) ||
                (x.Industry != null && x.Industry.Contains(value)) || (x.Summary != null && x.Summary.Contains(value)));
        }
        if (!string.IsNullOrWhiteSpace(company))
        {
            var value = company.Trim();
            query = query.Where(x => x.CompanyName.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(location))
        {
            var value = location.Trim();
            query = query.Where(x => x.Location != null && x.Location.Contains(value));
        }
        foreach (var technology in SplitTerms(techStack))
        {
            var value = technology;
            query = query.Where(x => x.Title.Contains(value) ||
                (x.Summary != null && x.Summary.Contains(value)) || x.SkillsJson.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(workMode))
        {
            var value = workMode.Trim();
            query = query.Where(x => x.RemoteType != null && x.RemoteType.Contains(value));
        }
        if (minSalary is { } salary) query = query.Where(x => x.SalaryMax != null && x.SalaryMax >= salary);
        if (equityOnly) query = query.Where(x => x.EquityMax != null && x.EquityMax > 0);
        if (!string.IsNullOrWhiteSpace(fundingStage)) query = query.Where(x => x.FundingStage == fundingStage.Trim());
        if (!string.IsNullOrWhiteSpace(industry))
        {
            var value = industry.Trim();
            query = query.Where(x => x.Industry != null && x.Industry.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(employmentType))
        {
            var value = employmentType.Trim();
            query = query.Where(x => x.EmploymentType != null && x.EmploymentType.Contains(value));
        }
        if (postedWithinDays is > 0)
        {
            var since = Now.AddDays(-Math.Clamp(postedWithinDays.Value, 1, 365));
            query = query.Where(x => x.PostedAt != null && x.PostedAt >= since);
        }
        if (state is { } requestedState) query = query.Where(x => x.State == requestedState);
        query = sort.ToLowerInvariant() switch
        {
            "salary" => query.OrderByDescending(x => x.SalaryMax).ThenByDescending(x => x.PostedAt),
            "company" => query.OrderBy(x => x.CompanyName).ThenByDescending(x => x.PostedAt),
            "match" => query.OrderByDescending(x => x.MatchScore).ThenByDescending(x => x.PostedAt),
            _ => query.OrderByDescending(x => x.PostedAt).ThenBy(x => x.CompanyName)
        };
        return (await query.Take(take).ToListAsync(ct))
            .Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<WellfoundApplicationDto>> ApplicationsAsync(WellfoundApplicationState? state, CancellationToken ct)
    {
        var query = db.WellfoundApplications.Where(x => x.OwnerId == user.OwnerId);
        if (state is { } value) query = query.Where(x => x.State == value);
        var rows = await query.OrderByDescending(x => x.FitScore).ThenByDescending(x => x.UpdatedAt).ToListAsync(ct);
        var jobIds = rows.Select(x => x.JobId).Distinct().ToArray();
        var titles = await db.WellfoundJobs.Where(x => x.OwnerId == user.OwnerId && jobIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Title, ct);
        return rows.Select(x => new WellfoundApplicationDto(x.Id, x.JobId, titles.GetValueOrDefault(x.JobId, "Wellfound job"),
            x.ProviderApplicationId, x.CandidateName, x.FitScore, x.State, x.IsDemo, x.Version, x.UpdatedAt)).ToList();
    }

    public async Task<IReadOnlyList<WellfoundActivityDto>> ActivitiesAsync(int take, CancellationToken ct) =>
        (await db.WellfoundActivities.Where(x => x.OwnerId == user.OwnerId).OrderByDescending(x => x.OccurredAt)
            .Take(Math.Clamp(take, 1, 200)).ToListAsync(ct))
        .Select(x => new WellfoundActivityDto(x.Id, x.Kind, x.Detail, x.ProviderConfirmed, x.OccurredAt)).ToList();

    public async Task<WellfoundJobDto> ChangeJobStateAsync(Guid id, ChangeWellfoundJobStateRequest request, CancellationToken ct)
    {
        var job = await db.WellfoundJobs.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == user.OwnerId, ct)
            ?? throw new NotFoundException("Wellfound job not found.");
        try { job.ChangeState(request.State, request.ExpectedVersion, Now); }
        catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }
        db.WellfoundActivities.Add(new(user.OwnerId, WellfoundActivityKind.JobStateChanged,
            $"{(job.IsDemo ? "Demo" : "Local")} job marked {request.State}.", Now, jobId: job.Id));
        await db.SaveChangesAsync(ct);
        return ToDto(job);
    }

    public async Task<WellfoundApplicationDto> ChangeApplicationStateAsync(Guid id, ChangeWellfoundApplicationStateRequest request, CancellationToken ct)
    {
        var application = await db.WellfoundApplications.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == user.OwnerId, ct)
            ?? throw new NotFoundException("Wellfound application not found.");
        if (!application.IsDemo)
            throw new ConflictException("Connect Wellfound Recruit and approve the exact provider action before changing a live application.");
        if (!request.Confirmed)
            throw Invalid("confirmed", "Confirm this demo decision before recording it.");
        try { application.ChangeState(request.State, request.ExpectedVersion, Now); }
        catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }
        db.WellfoundActivities.Add(new(user.OwnerId, WellfoundActivityKind.ApplicationStateChanged,
            $"Demo application marked {request.State}; no provider action occurred.", Now, applicationId: application.Id));
        await db.SaveChangesAsync(ct);
        var job = await db.WellfoundJobs.FirstAsync(x => x.Id == application.JobId && x.OwnerId == user.OwnerId, ct);
        return new(application.Id, application.JobId, job.Title, application.ProviderApplicationId,
            application.CandidateName, application.FitScore, application.State, true, application.Version, application.UpdatedAt);
    }

    public async Task<WellfoundKpisDto> KpisAsync(string workspace, CancellationToken ct)
    {
        var isSales = workspace.Equals("Sales", StringComparison.OrdinalIgnoreCase);
        var allJobs = await db.WellfoundJobs.Where(x => x.OwnerId == user.OwnerId).ToListAsync(ct);
        var jobs = allJobs.Where(x => x.Scope == (isSales ? WellfoundJobScope.RecruiterOwned : WellfoundJobScope.CandidateDiscovery)).ToList();
        var apps = isSales ? await db.WellfoundApplications.Where(x => x.OwnerId == user.OwnerId).ToListAsync(ct) : [];
        var activities = await db.WellfoundActivities.CountAsync(x => x.OwnerId == user.OwnerId, ct);
        return new(isSales ? "Sales" : "Candidate", jobs.Count,
            jobs.Count(x => x.State == WellfoundJobState.Saved), jobs.Count(x => x.State == WellfoundJobState.Applied),
            jobs.Count(x => x.State == WellfoundJobState.Interviewing), jobs.Count(x => x.State == WellfoundJobState.Offered),
            apps.Count, apps.Count(x => x.State == WellfoundApplicationState.Reviewing),
            apps.Count(x => x.State == WellfoundApplicationState.Shortlisted),
            apps.Count(x => x.State == WellfoundApplicationState.Rejected), activities,
            allJobs.Count(x => x.Scope == WellfoundJobScope.CandidateDiscovery));
    }

    public async Task<LoadWellfoundDemoResult> LoadDemoAsync(CancellationToken ct)
    {
        if (await db.WellfoundJobs.AnyAsync(x => x.OwnerId == user.OwnerId && x.IsDemo, ct)) return new(0, 0);
        var now = Now;
        var candidates = new[]
        {
            DemoJob("wf-demo-1", WellfoundJobScope.CandidateDiscovery, "Senior Backend Engineer", "Stripe", "Remote · Worldwide", "Fully Remote", 120000, 180000, .05m, .20m, "5+ years", "Full time", "FinTech", "Series E", "500–1000", true, 92, ["Python", "FastAPI", "PostgreSQL", "AWS"], now.AddDays(-2)),
            DemoJob("wf-demo-2", WellfoundJobScope.CandidateDiscovery, "Founding Full-Stack Engineer", "Orbit Labs", "India / Singapore", "Remote", 70000, 110000, .20m, .80m, "4+ years", "Full time", "Developer Tools", "Seed", "11–50", true, 88, ["TypeScript", "React", "Node.js", "PostgreSQL"], now.AddDays(-1)),
            DemoJob("wf-demo-3", WellfoundJobScope.CandidateDiscovery, "Platform Engineer", "Northstar AI", "Bengaluru", "Hybrid", 3500000, 5200000, .02m, .10m, "5+ years", "Full time", "AI Infrastructure", "Series B", "51–200", false, 81, ["Kubernetes", "Terraform", "AWS", "Golang"], now.AddDays(-4)),
            DemoJob("wf-demo-4", WellfoundJobScope.CandidateDiscovery, "Product Designer", "Caregrid", "Europe", "Fully Remote", 85000, 125000, .05m, .15m, "3+ years", "Full time", "HealthTech", "Series A", "51–200", true, 76, ["Figma", "Design Systems", "Research"], now.AddDays(-3)),
            DemoJob("wf-demo-sales-1", WellfoundJobScope.RecruiterOwned, "Senior Backend Engineer", "Demo company", "Remote", "Fully Remote", 120000, 180000, .05m, .20m, "5+ years", "Full time", "FinTech", "Series A", "51–200", true, null, ["Python", "PostgreSQL", "AWS"], now.AddDays(-5)),
            DemoJob("wf-demo-sales-2", WellfoundJobScope.RecruiterOwned, "React Engineer", "Demo company", "India", "Remote", 3000000, 4500000, .02m, .08m, "4+ years", "Full time", "SaaS", "Seed", "11–50", false, null, ["React", "TypeScript", "Testing"], now.AddDays(-3)),
        };
        db.WellfoundJobs.AddRange(candidates);
        var sales = candidates.Where(x => x.Scope == WellfoundJobScope.RecruiterOwned).ToArray();
        var applications = new[]
        {
            new WellfoundApplication(user.OwnerId, sales[0].Id, "wf-app-demo-1", "Aarav Mehta", 91, true, now),
            new WellfoundApplication(user.OwnerId, sales[0].Id, "wf-app-demo-2", "Maya Chen", 84, true, now),
            new WellfoundApplication(user.OwnerId, sales[1].Id, "wf-app-demo-3", "Riya Sharma", 88, true, now),
            new WellfoundApplication(user.OwnerId, sales[1].Id, "wf-app-demo-4", "Daniel Kim", 73, true, now),
        };
        db.WellfoundApplications.AddRange(applications);
        db.WellfoundActivities.Add(new(user.OwnerId, WellfoundActivityKind.Imported,
            "Loaded the labelled Wellfound demo dataset; no provider data was fetched.", now));
        await db.SaveChangesAsync(ct);
        return new(candidates.Length, applications.Length);
    }

    private WellfoundJob DemoJob(string providerId, WellfoundJobScope scope, string title, string company,
        string location, string remote, decimal salaryMin, decimal salaryMax, decimal equityMin, decimal equityMax,
        string experience, string employment, string industry, string funding, string employees, bool visa,
        int? match, string[] skills, DateTime postedAt)
    {
        var job = new WellfoundJob(user.OwnerId, providerId, scope, title, company,
            "https://wellfound.com/jobs", true, Now);
        job.SetDetails(location, remote, salaryMin, salaryMax, salaryMax > 1_000_000 ? "INR" : "USD",
            equityMin, equityMax, experience, employment, industry, funding, employees, visa, postedAt,
            $"Demo {title} opportunity used to verify filters, decisions and KPIs before live OAuth.",
            JsonSerializer.Serialize(skills, JsonOptions), JsonSerializer.Serialize(new { source = "Demo", providerId }, JsonOptions), match, Now);
        return job;
    }

    private static WellfoundJobDto ToDto(WellfoundJob x) => new(x.Id, x.ProviderJobId, x.Scope, x.Title,
        x.CompanyName, x.Location, x.RemoteType, x.SalaryMin, x.SalaryMax, x.Currency, x.EquityMin, x.EquityMax,
        x.ExperienceLevel, x.EmploymentType, x.Industry, x.FundingStage, x.EmployeeCount, x.VisaSponsorship,
        x.PostedAt, x.ApplyUrl, x.Summary, DeserializeStrings(x.SkillsJson), x.MatchScore, x.State, x.IsDemo,
        x.Version, x.UpdatedAt);

    private static IReadOnlyList<string> DeserializeStrings(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    private static IEnumerable<string> SplitTerms(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(12);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
