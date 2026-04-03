using Granit.BackgroundJobs;
using Granit.Modularity;
using Granit.Scheduling.Wolverine;

namespace Granit.Scheduling.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for Scheduling:
/// catch-up safety net for overdue scheduled actions.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitSchedulingModule),
    typeof(GranitSchedulingWolverineModule))]
public sealed class GranitSchedulingBackgroundJobsModule : GranitModule;
