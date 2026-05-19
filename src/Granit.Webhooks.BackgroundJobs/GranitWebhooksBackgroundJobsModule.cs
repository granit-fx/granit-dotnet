using Granit.BackgroundJobs;
using Granit.Modularity;
using Granit.Webhooks.BackgroundJobs.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Webhooks.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for Webhooks: the daily signing-key
/// rotation scanner that surfaces keys approaching expiration before consumers stop
/// being able to verify signatures.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitWebhooksModule))]
public sealed class GranitWebhooksBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddTransient<SigningKeyRotationScanService>();
}
