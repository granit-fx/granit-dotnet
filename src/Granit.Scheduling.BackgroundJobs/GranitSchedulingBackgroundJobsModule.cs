using Granit.BackgroundJobs;
using Granit.Modularity;
using Granit.Scheduling.BackgroundJobs.Services;
using Granit.Scheduling.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Scheduling.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for Scheduling:
/// catch-up safety net for overdue scheduled actions.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitSchedulingModule),
    typeof(GranitSchedulingWolverineModule))]
public sealed class GranitSchedulingBackgroundJobsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddTransient<CatchUpDispatcher>();
    }
}
