using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Abstractions;
using OpportunityPilot.Application.Ai;
using OpportunityPilot.Application.Configuration;
using OpportunityPilot.Domain.Ai;
using OpportunityPilot.Domain.Profiles;
using OpportunityPilot.Infrastructure.Persistence;

namespace OpportunityPilot.UnitTests;

public class AiServiceTests
{
    private sealed class Owner(Guid id) : ICurrentUser { public Guid OwnerId => id; }

    private sealed class ScriptedLlm(Func<LlmResult> respond) : ILlmClient
    {
        public int Calls { get; private set; }
        public Task<LlmResult> GenerateJsonAsync(string operation, string system, string input, CancellationToken ct) { Calls++; return Task.FromResult(respond()); }
    }

    private const string GoodJson = """{"mode":"Job","keywords":["Backend engineer"],"requiredSkills":["C#"],"locations":["Pune"]}""";

    private static async Task<(AiService Service, InMemoryAppDbContext Db, Guid ProfileId, ScriptedLlm Llm)> Create(Func<LlmResult> respond, int dailyLimit = 3,
        bool enabled = true, string? key = "test-key")
    {
        var db = new InMemoryAppDbContext(new DbContextOptionsBuilder<InMemoryAppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var owner = Guid.NewGuid();
        var profile = new Profile(owner, ProfileType.Candidate, "P", "{}", true, DateTime.UtcNow);
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        var llm = new ScriptedLlm(respond);
        var service = new AiService(db, new Owner(owner), llm, Options.Create(new FeatureOptions { GeminiEnabled = enabled }),
            Options.Create(new AiOptions { GeminiApiKey = key, DailyCallLimit = dailyLimit }), TimeProvider.System);
        return (service, db, profile.Id, llm);
    }

    private static GoalPreviewRequest Goal(Guid profileId) => new(profileId, "Backend engineer roles in Pune, 5 years C#", null);

    [Fact]
    public async Task A_valid_answer_is_used_and_recorded_without_the_prompt_or_answer()
    {
        var (service, db, profileId, _) = await Create(() => new LlmResult(GoodJson, AiOutcome.Succeeded, null, 1, TimeSpan.FromMilliseconds(120)));
        var preview = await service.PreviewGoalAsync(Goal(profileId), default);
        Assert.Equal(AiSource.Gemini, preview.Source);
        var usage = Assert.Single(db.AiUsages);
        Assert.Equal(AiOutcome.Succeeded, usage.Outcome);
        Assert.Null(usage.Reason);
        Assert.Equal(120, usage.DurationMs);
    }

    [Fact]
    public async Task The_daily_limit_falls_back_to_rules_without_calling_the_provider()
    {
        var (service, db, profileId, llm) = await Create(() => new LlmResult(GoodJson, AiOutcome.Succeeded, null, 1, TimeSpan.Zero), dailyLimit: 2);
        await service.PreviewGoalAsync(Goal(profileId), default);
        await service.PreviewGoalAsync(Goal(profileId), default);
        var third = await service.PreviewGoalAsync(Goal(profileId), default);
        Assert.Equal(AiSource.Rules, third.Source);
        Assert.Equal("DailyLimit", third.FallbackReason);
        Assert.Equal(2, llm.Calls);
        var status = await service.StatusAsync(default);
        Assert.Equal(2, status.CallsToday);
        Assert.Equal("DailyLimit", status.FallbackReason);
        Assert.Equal(1, db.AiUsages.Count(u => u.Outcome == AiOutcome.OverDailyLimit));
    }

    [Theory]
    [InlineData(AiOutcome.Timeout, "Timeout")]
    [InlineData(AiOutcome.ProviderError, "ProviderError")]
    public async Task Provider_failures_fall_back_to_rules_with_a_visible_reason(AiOutcome outcome, string reason)
    {
        var (service, _, profileId, _) = await Create(() => new LlmResult(null, outcome, "Gemini answered 503", 2, TimeSpan.FromSeconds(3)));
        var preview = await service.PreviewGoalAsync(Goal(profileId), default);
        Assert.Equal(AiSource.Rules, preview.Source);
        Assert.Equal(reason, preview.FallbackReason);
        Assert.NotEmpty(preview.Criteria.Keywords);
        var status = await service.StatusAsync(default);
        Assert.NotNull(status.LastFailure);
    }

    [Fact]
    public async Task An_answer_that_is_not_criteria_json_is_recorded_as_invalid_and_rules_are_used()
    {
        var (service, db, profileId, _) = await Create(() => new LlmResult("not json", AiOutcome.Succeeded, null, 1, TimeSpan.Zero));
        var preview = await service.PreviewGoalAsync(Goal(profileId), default);
        Assert.Equal("InvalidResponse", preview.FallbackReason);
        Assert.Equal(AiOutcome.InvalidResponse, Assert.Single(db.AiUsages).Outcome);
    }

    [Fact]
    public async Task Without_a_key_nothing_is_called_or_recorded()
    {
        var (service, db, profileId, llm) = await Create(() => throw new InvalidOperationException(), key: null);
        Assert.Equal("NoKey", (await service.PreviewGoalAsync(Goal(profileId), default)).FallbackReason);
        Assert.Equal(0, llm.Calls);
        Assert.Empty(db.AiUsages);
    }
}
