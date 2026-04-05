using Granit.BackgroundJobs;
using Granit.BlobStorage.BackgroundJobs.Services;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.BlobStorage.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for BlobStorage:
/// orphan blob cleanup.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitBlobStorageModule))]
public sealed class GranitBlobStorageBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddTransient<OrphanBlobCleanupService>();
}
