using Granit.Entities.Views.Extensions;
using Granit.Modularity;

namespace Granit.Entities.Views;

/// <summary>
/// Granit module for the EntityView runtime domain (aggregate + value objects + validation).
/// Pull this module from a host that resolves the EntityView aggregate; persistence (EF Core)
/// and endpoints ship as siblings (<c>Granit.Entities.Views.EntityFrameworkCore</c>,
/// <c>Granit.Entities.Views.Endpoints</c>).
/// </summary>
/// <remarks>
/// Registers a default no-op <see cref="IEntityViewReader"/> so hosts that omit the EF
/// companion still boot. The EF module replaces the binding via <c>services.Replace(...)</c>.
/// </remarks>
[DependsOn(typeof(GranitEntitiesViewsAbstractionsModule))]
public sealed class GranitEntitiesViewsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitEntitiesViews();
}
