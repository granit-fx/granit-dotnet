using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Entities.Views.EntityFrameworkCore;

/// <summary>
/// Granit module for EntityView persistence — the isolated DbContext, the
/// entity-type configuration, and the <c>IEntityViewReader</c> / <c>IEntityViewWriter</c>
/// implementations gated by the closed permission set of <c>EntityViewPermissions</c>
/// (per ADR-047 §6).
/// </summary>
/// <remarks>
/// The application must wire the DbContext provider via
/// <c>AddGranitEntitiesViewsEntityFrameworkCore(opts =&gt; opts.UseNpgsql(connectionString))</c>
/// — this module does not register a default in-memory provider so consumers stay
/// portable across SQL backends.
/// </remarks>
[DependsOn(
    typeof(GranitEntitiesViewsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitEntitiesViewsEntityFrameworkCoreModule : GranitModule
{
    // Services are registered via AddGranitEntitiesViewsEntityFrameworkCore() extension
    // method because it requires the DbContext configuration callback.
}
