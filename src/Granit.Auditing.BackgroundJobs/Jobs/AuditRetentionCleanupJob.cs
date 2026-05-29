using Granit.BackgroundJobs;

namespace Granit.Auditing.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that purges expired audit log entries based on per-category
/// retention periods.
/// </summary>
/// <remarks>
/// Runs <b>once cluster-wide</b> via the distributed scheduler, replacing the
/// former per-pod <c>AuditingCleanupWorker</c> hosted service (which purged N×
/// in parallel on a multi-replica deployment). Default schedule is daily at
/// 02:00; override via <c>BackgroundJobs:Jobs:auditing-retention-cleanup</c>.
/// </remarks>
[RecurringJob("0 2 * * *", "auditing-retention-cleanup")]
public sealed record AuditRetentionCleanupJob : IBackgroundJob;
