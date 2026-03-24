using Granit.Modularity;
using Granit.Persistence;

namespace Granit.Timeline.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence in the timeline engine.
/// </summary>
/// <remarks>
/// Replaces the default InMemory stores with durable PostgreSQL implementations.
/// The application must configure the DbContext via
/// <c>AddGranitTimelineEntityFrameworkCore(opts => opts.UseNpgsql(connectionString))</c>
/// instead of using this module directly when custom DbContext options are needed.
/// </remarks>
[DependsOn(
    typeof(GranitPersistenceModule),
    typeof(GranitTimelineModule))]
public sealed class GranitTimelineEntityFrameworkCoreModule : GranitModule
{
    // Services are registered via AddGranitTimelineEntityFrameworkCore() extension method
    // because it requires the DbContext configuration callback.
}
