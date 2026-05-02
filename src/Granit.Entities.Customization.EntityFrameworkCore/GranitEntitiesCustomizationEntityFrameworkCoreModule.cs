using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Entities.Customization.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence in the entities-customization runtime.
/// Pulls in <see cref="GranitEntitiesCustomizationModule"/> +
/// <see cref="GranitPersistenceEntityFrameworkCoreModule"/>; the host application
/// wires the actual provider via
/// <c>AddGranitEntitiesCustomizationEntityFrameworkCore(opts =&gt; opts.UseNpgsql(...))</c>.
/// </summary>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitEntitiesCustomizationModule))]
public sealed class GranitEntitiesCustomizationEntityFrameworkCoreModule : GranitModule;
