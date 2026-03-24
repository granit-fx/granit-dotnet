using Granit.BackgroundJobs;
using Granit.Modularity;

namespace Granit.Privacy.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for Privacy:
/// deletion deadline enforcement.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitPrivacyModule))]
public sealed class GranitPrivacyBackgroundJobsModule : GranitModule;
