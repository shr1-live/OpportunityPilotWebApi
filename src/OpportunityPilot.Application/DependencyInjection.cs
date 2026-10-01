using Microsoft.Extensions.DependencyInjection;
using OpportunityPilot.Application.Capabilities;
using OpportunityPilot.Application.Profiles;

namespace OpportunityPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProfileService>();
        services.AddSingleton<CapabilityService>();
        return services;
    }
}
