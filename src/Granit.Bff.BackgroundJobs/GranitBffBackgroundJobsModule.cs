using Granit.BackgroundJobs;
using Granit.Bff.EntityFrameworkCore;
using Granit.Modularity;

namespace Granit.Bff.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for BFF:
/// expired session cleanup for EF Core-backed deployments.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitBffEntityFrameworkCoreModule))]
public sealed class GranitBffBackgroundJobsModule : GranitModule;
