using Granit.BackgroundJobs;
using Granit.Modularity;

namespace Granit.BlobStorage.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for BlobStorage:
/// orphan blob cleanup.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitBlobStorageModule))]
public sealed class GranitBlobStorageBackgroundJobsModule : GranitModule;
