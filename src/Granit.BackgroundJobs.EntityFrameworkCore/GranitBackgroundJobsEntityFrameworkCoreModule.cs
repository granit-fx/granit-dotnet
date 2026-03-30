using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.BackgroundJobs.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of background jobs.
/// Registers <c>BackgroundJobsDbContext</c> and <c>EfBackgroundJobStore</c>.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitBackgroundJobsEntityFrameworkCoreModule : GranitModule;
