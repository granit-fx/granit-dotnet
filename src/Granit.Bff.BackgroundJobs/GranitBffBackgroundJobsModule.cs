using Granit.BackgroundJobs;
using Granit.Bff.BackgroundJobs.Internal;
using Granit.Bff.EntityFrameworkCore;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Bff.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for BFF:
/// expired session cleanup for EF Core-backed deployments.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitBffEntityFrameworkCoreModule))]
public sealed class GranitBffBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddTransient<ExpiredSessionCleanupService>();
}
