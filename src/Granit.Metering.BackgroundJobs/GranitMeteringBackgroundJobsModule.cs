using Granit.BackgroundJobs;
using Granit.Modularity;

namespace Granit.Metering.BackgroundJobs;

/// <summary>
/// Background jobs for metering: hourly aggregation with watermark-based
/// idempotency and periodic quota threshold checks.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitMeteringModule))]
public sealed class GranitMeteringBackgroundJobsModule : GranitModule;
