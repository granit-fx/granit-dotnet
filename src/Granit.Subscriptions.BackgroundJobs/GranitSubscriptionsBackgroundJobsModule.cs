using Granit.BackgroundJobs;
using Granit.Modularity;
using Granit.Subscriptions.BackgroundJobs.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Subscriptions.BackgroundJobs;

/// <summary>
/// Background jobs for subscription lifecycle: trial expiration, period end detection,
/// and cancel-at-period-end processing.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitSubscriptionsModule))]
public sealed class GranitSubscriptionsBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddTransient<TrialExpirationScanner>();
}
