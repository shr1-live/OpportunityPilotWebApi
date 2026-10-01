using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OpportunityPilot.IntegrationTests;

public class StartupGuardTests
{
    private sealed class ProductionWithDevBypass : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Main", "Host=unused;Database=unused");
            builder.UseSetting("Auth:DevBypass", "true");
            builder.UseSetting("Auth:SupabaseUrl", "https://example.supabase.co");
        }
    }

    private sealed class ProductionWithoutAuth : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Main", "Host=unused;Database=unused");
            builder.UseSetting("Auth:SupabaseUrl", "");
        }
    }

    [Fact]
    public void Dev_bypass_cannot_be_enabled_outside_development()
    {
        using var factory = new ProductionWithDevBypass();
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("DevBypass", ex.ToString());
    }

    [Fact]
    public void Production_without_supabase_auth_fails_with_a_setup_error()
    {
        using var factory = new ProductionWithoutAuth();
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Auth:SupabaseUrl", ex.ToString());
    }
}
