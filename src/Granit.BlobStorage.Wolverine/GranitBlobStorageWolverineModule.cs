using Granit.BackgroundJobs;
using Granit.Core.Modularity;

namespace Granit.BlobStorage.Wolverine;

/// <summary>
/// Granit module for blob storage background jobs.
/// Registers the <see cref="CleanupOrphanBlobsCommand"/> recurring job (hourly).
/// </summary>
/// <remarks>
/// The scheduling infrastructure is provided by <c>Granit.BackgroundJobs.Wolverine</c>
/// which automatically discovers <see cref="RecurringJobAttribute"/>-decorated commands.
/// </remarks>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitBlobStorageModule))]
public sealed class GranitBlobStorageWolverineModule : GranitModule;
