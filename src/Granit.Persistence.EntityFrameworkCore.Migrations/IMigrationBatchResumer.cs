namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Dispatches one batch command per pending or in-progress data-migration cycle so
/// interrupted cycles resume from their stored cursor.
/// </summary>
/// <remarks>
/// Deliberately lock-free: concurrency control (distributed lock, timeout, exit codes)
/// is owned by <see cref="IGranitMigrationRunner"/>, the only caller. This interface is
/// public for the same reason as <c>IMigrationProgressDbEnsurer</c> — the Hosting package
/// consumes it without seeing this package's internal <c>MigrationProgressDbContext</c>.
/// </remarks>
public interface IMigrationBatchResumer
{
    /// <summary>
    /// Queries pending and in-progress cycles and dispatches one
    /// <c>RunMigrationBatchCommand</c> per cycle (per tenant when an
    /// <see cref="ITenantEnumerator"/> yields tenants).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of dispatched commands.</returns>
    Task<int> ResumeAsync(CancellationToken cancellationToken);
}
