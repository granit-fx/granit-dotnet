using Granit.BackgroundJobs;

namespace Granit.Documents.BackgroundJobs.Jobs;

/// <summary>
/// F9.1 — recurring cleanup of blobs stuck in <c>Pending</c> / <c>Uploading</c>
/// for the documents container. Runs hourly. Delegates to
/// <see cref="IDocumentMaintenanceService.CleanupOrphanBlobsAsync"/> which in turn
/// calls <c>IBlobStorage.CleanupOrphansAsync</c> — the documents-side schedule keeps
/// the maintenance window co-located with the rest of the module's jobs and lets
/// hosts disable it without touching the BlobStorage schedule.
/// </summary>
[RecurringJob("0 * * * *", "documents-orphan-cleanup")]
public sealed record OrphanDocumentCleanupJob : IBackgroundJob;
