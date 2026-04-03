using Granit.BackgroundJobs;

namespace Granit.Metering.BackgroundJobs.Jobs;

/// <summary>
/// Checks current usage against plan-defined quotas and publishes
/// threshold (80%) and exceeded (100%) integration events.
/// </summary>
[RecurringJob("*/15 * * * *", "metering-quota-check")]
public sealed record QuotaThresholdCheckJob : IBackgroundJob;
