using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Domain.Agents;
using OpportunityPilot.Domain.Applications;
using OpportunityPilot.Domain.Profiles;

namespace OpportunityPilot.Application.Abstractions;

/// <summary>Narrow persistence boundary. EF Core already supplies change tracking and the unit of work.</summary>
public interface IAppDbContext
{
    DbSet<Profile> Profiles { get; }
    DbSet<JobApplication> JobApplications { get; }
    DbSet<AgentKey> AgentKeys { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
