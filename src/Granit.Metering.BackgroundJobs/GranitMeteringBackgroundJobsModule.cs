using Granit.BackgroundJobs;
using Granit.Metering.BackgroundJobs.Internal;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Metering.BackgroundJobs;

/// <summary>
/// Background jobs for metering: hourly aggregation with watermark-based
/// idempotency and periodic quota threshold checks.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitMeteringModule))]
public sealed class GranitMeteringBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddTransient<MeteringAggregationScanner>();
        context.Services.TryAddTransient<QuotaThresholdScanner>();
    }
}
