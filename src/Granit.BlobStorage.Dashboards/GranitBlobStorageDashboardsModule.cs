using Granit.Analytics;
using Granit.Dashboards;
using Granit.Dashboards.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.BlobStorage.Dashboards;

/// <summary>
/// Granit module for the BlobStorage dashboards satellite. Registers the
/// <see cref="BlobStorageOperationsDashboardDefinition"/> so admin hosts surface
/// it through the dashboards endpoints.
/// </summary>
[DependsOn(
    typeof(GranitDashboardsAbstractionsModule),
    typeof(GranitAnalyticsAbstractionsModule))]
public sealed class GranitBlobStorageDashboardsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddDashboardDefinition<BlobStorageOperationsDashboardDefinition>();
    }
}
