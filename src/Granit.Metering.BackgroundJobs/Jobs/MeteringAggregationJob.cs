using Granit.BackgroundJobs;

namespace Granit.Metering.BackgroundJobs.Jobs;

/// <summary>
/// Computes hourly rollups from raw meter events into usage aggregates.
/// Uses watermark-based cursor to ensure idempotent aggregation.
/// </summary>
[RecurringJob("0 */1 * * *", "metering-aggregation")]
public sealed record MeteringAggregationJob : IBackgroundJob;
