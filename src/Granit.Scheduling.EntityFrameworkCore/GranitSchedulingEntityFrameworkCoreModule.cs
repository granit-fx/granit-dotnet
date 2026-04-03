using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Scheduling.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of scheduled actions.
/// Registers <c>SchedulingDbContext</c> and <c>EfScheduledActionStore</c>.
/// </summary>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitSchedulingModule))]
public sealed class GranitSchedulingEntityFrameworkCoreModule : GranitModule;
