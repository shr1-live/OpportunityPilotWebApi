using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OpportunityPilot.IntegrationTests;

/// <summary>Browser preflights against the production CORS settings. No Docker needed (demo mode).</summary>
public class CorsTests
{
    private sealed class ProductionFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Main", "");
            builder.UseSetting("Auth:SupabaseUrl", "");
        }
    }

    private static async Task<string?> AllowedOriginFor(HttpClient client, string origin)
    {
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/guest");
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");
        var response = await client.SendAsync(preflight);
        Assert.True(response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK, $"preflight {response.StatusCode}");
        return response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;
    }

    [Theory]
    [InlineData("https://opportunity-pilot-webapp.vercel.app")]
    [InlineData("https://opportunity-pilot-webapp-awqb5g667-vsr6.vercel.app")]
    [InlineData("https://opportunity-pilot-webapp-git-feature-redesign-shell-vsr6.vercel.app")]
    public async Task The_production_site_and_its_own_vercel_deployments_are_allowed(string origin)
    {
        using var factory = new ProductionFactory();
        Assert.Equal(origin, await AllowedOriginFor(factory.CreateClient(), origin));
    }

    [Theory]
    [InlineData("https://evil.vercel.app")]
    [InlineData("https://opportunity-pilot-webapp-x-othert.vercel.app")]
    [InlineData("https://opportunity-pilot-webapp-x-vsr6.vercel.app.evil.com")]
    [InlineData("https://opportunity-pilot-webapp-a.b-vsr6.vercel.app")]
    [InlineData("http://opportunity-pilot-webapp-awqb5g667-vsr6.vercel.app")]
    public async Task Look_alike_origins_are_refused(string origin)
    {
        using var factory = new ProductionFactory();
        Assert.Null(await AllowedOriginFor(factory.CreateClient(), origin));
    }
}
