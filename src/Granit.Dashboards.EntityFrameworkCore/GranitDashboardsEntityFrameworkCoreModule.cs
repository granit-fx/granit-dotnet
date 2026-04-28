using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Dashboards.EntityFrameworkCore;

/// <summary>
/// Granit module for the dashboards EF Core persistence layer.
/// </summary>
/// <remarks>
/// This module ships the <c>DashboardsDbContext</c> and the EF configurations for
/// the <c>Dashboard</c> aggregate. Hosts wire it via
/// <c>builder.AddGranitDashboardsEntityFrameworkCore(options =&gt; options.UseNpgsql(...))</c>.
/// Migrations belong to the consuming application, never to the framework module
/// (per Granit conventions).
/// </remarks>
[DependsOn(
    typeof(GranitDashboardsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitDashboardsEntityFrameworkCoreModule : GranitModule;
