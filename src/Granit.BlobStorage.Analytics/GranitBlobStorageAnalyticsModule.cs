using Granit.Analytics;
using Granit.Analytics.Extensions;
using Granit.BlobStorage.Analytics.Metrics;
using Granit.BlobStorage.Domain;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.BlobStorage.Analytics;

/// <summary>
/// Granit module for the BlobStorage analytics satellite. Registers the
/// MetricDefinitions observing the <c>BlobDescriptor</c> population so analytics
/// hosts can surface them in dashboards and admin pages.
/// </summary>
[DependsOn(
    typeof(GranitBlobStorageModule),
    typeof(GranitAnalyticsAbstractionsModule))]
public sealed class GranitBlobStorageAnalyticsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMetricDefinition<BlobDescriptor, int, ValidBlobDescriptorCountMetricDefinition>();
        context.Services.AddMetricDefinition<BlobDescriptor, long, ValidBlobDescriptorSizeTotalMetricDefinition>();
        context.Services.AddMetricDefinition<BlobDescriptor, int, OrphanBlobDescriptorCountMetricDefinition>();
    }
}
