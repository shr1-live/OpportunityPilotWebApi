using Microsoft.Extensions.DependencyInjection;
using OpportunityPilot.Application.Agents;
using OpportunityPilot.Application.Analytics;
using OpportunityPilot.Application.Applications;
using OpportunityPilot.Application.Approvals;
using OpportunityPilot.Application.Auth;
using OpportunityPilot.Application.Campaigns;
using OpportunityPilot.Application.Capabilities;
using OpportunityPilot.Application.Drafts;
using OpportunityPilot.Application.Sales;
using OpportunityPilot.Application.Imports;
using OpportunityPilot.Application.Opportunities;
using OpportunityPilot.Application.Profiles;
using OpportunityPilot.Application.Research;
using OpportunityPilot.Application.Research.Boards;
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
        services.AddScoped<GuestSessionService>();
        services.AddScoped<CampaignService>();
        services.AddScoped<SourceService>();
        services.AddScoped<ImportService>();
        services.AddScoped<ResearchService>();
        services.AddScoped<OpportunityService>();
        services.AddScoped<AgentResearchService>();
        services.AddScoped<ApprovalService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<DraftService>();
        services.AddScoped<SalesService>();
        services.AddScoped<JobBoardGatherer>();
        services.AddScoped<IResearchRunner, ResearchRunner>();
        services.AddSingleton<CapabilityService>();
        return services;
    }
}
