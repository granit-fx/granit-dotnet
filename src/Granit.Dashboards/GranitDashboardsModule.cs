using Granit.Dashboards.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Dashboards;

/// <summary>
/// Granit module for the dashboard runtime. Registers the
/// <see cref="IDashboardDefinitionRegistry"/> and provides the
/// <c>AddDashboardDefinition&lt;TDefinition&gt;()</c> DI extension.
/// </summary>
/// <remarks>
/// Per-domain widget catalogues live in their own modules (Granit.Analytics ships
/// KPI / Chart / Table / Pivot widgets; future Granit.IoT.Dashboards will ship
/// gauge / camera / alarm widgets). Persistence (<c>Dashboard</c> aggregate) and
/// HTTP endpoints will land in <c>Granit.Dashboards.EntityFrameworkCore</c> /
/// <c>Granit.Dashboards.Endpoints</c> in subsequent stories.
/// </remarks>
[DependsOn(typeof(GranitDashboardsAbstractionsModule))]
public sealed class GranitDashboardsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDashboards();
}
