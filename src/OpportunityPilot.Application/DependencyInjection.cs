using Microsoft.Extensions.DependencyInjection;
using OpportunityPilot.Application.Agents;
using OpportunityPilot.Application.Applications;
using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Application.Capabilities;
using OpportunityPilot.Application.Imports;
using OpportunityPilot.Application.Opportunities;
using OpportunityPilot.Application.Profiles;
using OpportunityPilot.Application.Research;
using OpportunityPilot.Application.Sources;

namespace OpportunityPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProfileService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<AgentKeyService>();
        services.AddScoped<CampaignService>();
        services.AddScoped<SourceService>();
        services.AddScoped<ImportService>();
        services.AddScoped<ResearchService>();
        services.AddScoped<OpportunityService>();
        services.AddScoped<AgentResearchService>();
        services.AddScoped<IResearchRunner, ResearchRunner>();
        services.AddSingleton<CapabilityService>();
        return services;
    }
}
