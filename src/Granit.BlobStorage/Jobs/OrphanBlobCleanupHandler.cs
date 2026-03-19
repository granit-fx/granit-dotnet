using Microsoft.Extensions.Logging;

namespace Granit.BlobStorage.Jobs;

/// <summary>
/// Handler for <see cref="OrphanBlobCleanupJob"/>.
/// Delegates to <see cref="IBlobStorage.CleanupOrphansAsync"/> to clean up
/// blobs stuck in Pending/Uploading for over 24 hours.
/// </summary>
internal static partial class OrphanBlobCleanupHandler
{
    public static async Task HandleAsync(
        OrphanBlobCleanupJob job,
        IBlobStorage blobStorage,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        int cleaned = await blobStorage
            .CleanupOrphansAsync(cancellationToken)
            .ConfigureAwait(false);

        if (cleaned > 0)
        {
            LogOrphansCleaned(logger, cleaned);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Cleaned up {Count} orphaned blob(s).")]
    private static partial void LogOrphansCleaned(ILogger logger, int count);
}
