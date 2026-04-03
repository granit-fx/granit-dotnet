using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of metering data.
/// </summary>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitMeteringModule))]
public sealed class GranitMeteringEntityFrameworkCoreModule : GranitModule;
