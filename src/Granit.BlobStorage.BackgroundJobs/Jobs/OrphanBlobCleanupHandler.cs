using Granit.BlobStorage.BackgroundJobs.Internal;

namespace Granit.BlobStorage.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OrphanBlobCleanupJob"/>. Delegates to
/// <see cref="OrphanBlobCleanupService"/> for orphan blob cleanup logic.
/// </summary>
internal static class OrphanBlobCleanupHandler
{
    public static Task HandleAsync(
        OrphanBlobCleanupJob _,
        OrphanBlobCleanupService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
