using Microsoft.Extensions.Logging;

namespace Granit.BlobStorage.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OrphanBlobCleanupJob"/>.
/// Delegates to <see cref="IBlobStorage.CleanupOrphansAsync"/> to clean up
/// blobs stuck in Pending/Uploading for over 24 hours.
/// </summary>
internal static partial class OrphanBlobCleanupHandler
{
    public static async Task HandleAsync(
        OrphanBlobCleanupJob _,
        IBlobStorage blobStorage,
        ILogger<OrphanBlobCleanupJob> logger,
        CancellationToken cancellationToken)
    {
        int cleaned = await blobStorage
            .CleanupOrphansAsync(cancellationToken)
            .ConfigureAwait(false);

        if (cleaned > 0)
        {
            Log.OrphansCleaned(logger, cleaned);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Cleaned up {Count} orphaned blob(s).")]
        public static partial void OrphansCleaned(ILogger logger, int count);
    }
}
