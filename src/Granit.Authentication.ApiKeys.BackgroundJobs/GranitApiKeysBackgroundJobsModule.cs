using Granit.Authentication.ApiKeys.BackgroundJobs.Services;
using Granit.BackgroundJobs;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authentication.ApiKeys.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for API keys: a daily scanner that
/// emits <c>ApiKeyExpiringSoonEto</c> for keys approaching expiration. Pairs with
/// <c>Granit.Authentication.ApiKeys.Notifications</c> to deliver proactive rotation
/// reminders to tenant administrators (ISO 27001 A.9.4).
/// </summary>
[DependsOn(
    typeof(GranitAuthenticationApiKeysModule),
    typeof(GranitBackgroundJobsModule))]
public sealed class GranitApiKeysBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddTransient<ExpiringApiKeyScannerService>();
}
