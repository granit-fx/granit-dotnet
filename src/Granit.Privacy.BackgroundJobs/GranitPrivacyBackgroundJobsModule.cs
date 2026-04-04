using Granit.BackgroundJobs;
using Granit.Modularity;
using Granit.Privacy.BackgroundJobs.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Privacy.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for Privacy:
/// deletion deadline enforcement.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitPrivacyModule))]
public sealed class GranitPrivacyBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddTransient<DeletionDeadlineEnforcementService>();
}
