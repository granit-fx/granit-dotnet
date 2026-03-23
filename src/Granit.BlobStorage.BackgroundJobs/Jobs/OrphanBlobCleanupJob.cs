using Granit.BackgroundJobs;

namespace Granit.BlobStorage.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that cleans up blobs stuck in Pending/Uploading for over 24 hours.
/// Runs hourly via <see cref="RecurringJobAttribute"/>.
/// </summary>
[RecurringJob("0 * * * *", "blob-storage-orphan-cleanup")]
public sealed record OrphanBlobCleanupJob : IBackgroundJob;
