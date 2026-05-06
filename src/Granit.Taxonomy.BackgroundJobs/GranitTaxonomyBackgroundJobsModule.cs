using Granit.BackgroundJobs;
using Granit.Modularity;
using Granit.Taxonomy;

namespace Granit.Taxonomy.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for the Taxonomy module:
/// nightly orphan-assignment cleanup (T5.2).
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitTaxonomyModule))]
public sealed class GranitTaxonomyBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // The job's handler resolves IOrphanAssignmentSweepService from DI; the
        // implementation is registered by AddGranitTaxonomyEntityFrameworkCore.
    }
}
