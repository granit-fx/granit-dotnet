using Granit.Entities.Views.EntityFrameworkCore.Extensions;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Entities.Views.EntityFrameworkCore;

/// <summary>
/// Granit module for EntityView persistence — the isolated DbContext, the
/// entity-type configuration, and the <c>IEntityViewReader</c> / <c>IEntityViewWriter</c>
/// implementations gated by the closed permission set of <c>EntityViewPermissions</c>
/// (per ADR-047 §6).
/// </summary>
[DependsOn(
    typeof(GranitEntitiesViewsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitEntitiesViewsEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitEntitiesViewsEntityFrameworkCore();
}
