using Microsoft.Extensions.Logging;

namespace Granit.BlobStorage.BackgroundJobs.Services;

/// <summary>
/// Cleans up blobs stuck in Pending/Uploading for over 24 hours.
/// </summary>
public sealed partial class OrphanBlobCleanupService(
    IBlobStorage blobStorage,
    ILogger<OrphanBlobCleanupService> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
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
