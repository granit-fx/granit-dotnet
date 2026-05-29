namespace Granit.Auditing.BackgroundJobs;

/// <summary>
/// Purges expired audit log entries across every <c>AuditCategory</c> according
/// to its configured retention period.
/// </summary>
public interface IAuditRetentionCleanupService
{
    /// <summary>
    /// Runs one full retention sweep: for each category, deletes entries older
    /// than its retention cutoff in batches.
    /// </summary>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
