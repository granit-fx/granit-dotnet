using Granit.BackgroundJobs;

namespace Granit.BlobStorage.Wolverine;

/// <summary>
/// Wolverine message triggering the orphan blob cleanup job.
/// Runs hourly via <see cref="RecurringJobAttribute"/>.
/// </summary>
[RecurringJob("0 * * * *", "blob-orphan-cleanup")]
public sealed record CleanupOrphanBlobsCommand;
