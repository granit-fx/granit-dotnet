using Granit.Modularity;

namespace Granit.Entities.Views;

/// <summary>
/// Granit module for the EntityView runtime domain (aggregate + value objects + validation).
/// Pull this module from a host that resolves the EntityView aggregate; persistence (EF Core)
/// and endpoints ship as siblings (<c>Granit.Entities.Views.EntityFrameworkCore</c>,
/// <c>Granit.Entities.Views.Endpoints</c>).
/// </summary>
[DependsOn(typeof(GranitEntitiesViewsAbstractionsModule))]
public sealed class GranitEntitiesViewsModule : GranitModule;
