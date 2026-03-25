using Granit.BackgroundJobs;
using Granit.Caching;
using Granit.Modularity;
using Granit.Settings;

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
public sealed class GranitOpenIddictBackgroundJobsModule : GranitModule;
