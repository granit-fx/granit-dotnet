using Granit.BackgroundJobs;
using Granit.Core.Modularity;

namespace Granit.OpenIddict.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for OpenIddict:
/// token cleanup and idle session enforcement.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitOpenIddictModule))]
public sealed class GranitOpenIddictBackgroundJobsModule : GranitModule;
