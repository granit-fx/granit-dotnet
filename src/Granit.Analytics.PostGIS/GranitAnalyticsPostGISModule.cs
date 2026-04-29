using Granit.Analytics.PostGIS.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Analytics.PostGIS;

/// <summary>
/// Granit module for the PostGIS / NetTopologySuite-based geography projector.
/// Once loaded, the host's <c>MapWidget</c>s can use
/// <see cref="Analytics.Dashboards.Widgets.MapPointSource.Geography"/> on any
/// entity that exposes a <c>NetTopologySuite.Geometries.Point</c> column.
/// </summary>
[DependsOn(typeof(GranitAnalyticsModule))]
public sealed class GranitAnalyticsPostGISModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitAnalyticsPostGIS();
}
