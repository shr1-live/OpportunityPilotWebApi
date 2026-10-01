using Microsoft.Extensions.Options;
using OpportunityPilot.Application.Capabilities;
using OpportunityPilot.Application.Configuration;

namespace OpportunityPilot.UnitTests;

public class CapabilityServiceTests
{
    private static CapabilitiesDto Get(FeatureOptions f, AiOptions a) =>
        new CapabilityService(Options.Create(f), Options.Create(a), Options.Create(new DatabaseOptions { Provider = "Postgres" }), new SetupState())
            .Get("Test");

    [Fact]
    public void Without_a_key_gemini_is_not_configured_and_rules_mode_applies()
    {
        var caps = Get(new FeatureOptions { GeminiEnabled = true }, new AiOptions { GeminiApiKey = null });

        Assert.Equal(CapabilityStatus.NotConfigured, caps.Items.Single(i => i.Key == "gemini").Status);
        Assert.Equal("Rules", caps.AiMode);
    }

    [Fact]
    public void A_key_alone_never_reports_gemini_as_ready()
    {
        var caps = Get(new FeatureOptions { GeminiEnabled = true }, new AiOptions { GeminiApiKey = "secret-value" });

        var gemini = caps.Items.Single(i => i.Key == "gemini");
        Assert.Equal(CapabilityStatus.Configured, gemini.Status);
        Assert.DoesNotContain("secret-value", gemini.Detail);
    }

    [Theory]
    [InlineData("linkedin", CapabilityStatus.LocalAgent)]
    [InlineData("naukri", CapabilityStatus.LocalAgent)]
    [InlineData("instahyre", CapabilityStatus.ManualHandoff)]
    public void Job_platforms_report_how_applications_happen(string key, CapabilityStatus expected)
    {
        var caps = Get(new FeatureOptions(), new AiOptions());
        Assert.Equal(expected, caps.Items.Single(i => i.Key == key).Status);
    }

    [Theory]
    [InlineData("linkedin")]
    [InlineData("naukri")]
    public void Agent_platforms_warn_that_automation_is_unofficial(string key)
    {
        var item = Get(new FeatureOptions(), new AiOptions()).Items.Single(i => i.Key == key);
        Assert.Contains("Unofficial automation", item.Detail);
    }
}
