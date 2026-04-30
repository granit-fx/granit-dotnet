using Granit.Dashboards.Push.Extensions;
using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.Validation;

namespace Granit.Dashboards.Push;

/// <summary>
/// Granit module for the dashboards SSE push transport. Wires the in-memory
/// hub + the <see cref="IWidgetPushPublisher"/> producer contract. ADR-043.
/// </summary>
/// <remarks>
/// Hosts that need live dashboard channels add this module to their composition
/// root and call <c>app.MapGranitDashboardsPush()</c> alongside the standard
/// <c>app.MapGranitDashboards()</c>. Hosts that stay pull-only skip this module
/// — the framework degrades <c>Realtime</c> widgets to <c>Dynamic</c> cadence
/// with no runtime breakage.
/// </remarks>
[DependsOn(
    typeof(GranitDashboardsModule),
    typeof(GranitMultiTenancyModule),
    typeof(GranitValidationModule))]
public sealed class GranitDashboardsPushModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDashboardsPush();
}
