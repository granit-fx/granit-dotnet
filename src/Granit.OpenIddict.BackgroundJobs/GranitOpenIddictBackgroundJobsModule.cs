using Granit.BackgroundJobs;
using Granit.Caching;
using Granit.Modularity;
using Granit.OpenIddict.BackgroundJobs.Services;
using Granit.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.OpenIddict.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for OpenIddict:
/// token cleanup, idle session enforcement, and signing key rotation.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitCachingModule),
    typeof(GranitOpenIddictModule),
    typeof(GranitSettingsModule))]
public sealed class GranitOpenIddictBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddTransient<IdleSessionEnforcementService>();
        context.Services.TryAddTransient<KeyRotationExecutionService>();
        context.Services.TryAddTransient<TokenCleanupService>();
    }
}
