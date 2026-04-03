using Granit.BackgroundJobs;
using Granit.Modularity;

namespace Granit.Subscriptions.BackgroundJobs;

/// <summary>
/// Background jobs for subscription lifecycle: trial expiration, period end detection,
/// and cancel-at-period-end processing.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitSubscriptionsModule))]
public sealed class GranitSubscriptionsBackgroundJobsModule : GranitModule;
