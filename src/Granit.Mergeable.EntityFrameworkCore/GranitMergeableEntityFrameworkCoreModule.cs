using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Mergeable.EntityFrameworkCore;

/// <summary>
/// Granit module marker for the EF Core merge orchestrator package. Wired via
/// <c>builder.AddGranitMergeableEntityFrameworkCore(...)</c>.
/// </summary>
[DependsOn(typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitMergeableEntityFrameworkCoreModule : GranitModule;
