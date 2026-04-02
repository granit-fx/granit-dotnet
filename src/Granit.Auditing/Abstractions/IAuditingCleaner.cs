using Granit.Auditing.Domain;

namespace Granit.Auditing.Abstractions;

/// <summary>
/// Purges expired audit entries from the underlying store.
/// </summary>
/// <remarks>
/// Implemented by the persistence layer (e.g. EF Core) and consumed by the
/// <c>AuditingCleanupWorker</c> background service.
/// </remarks>
public interface IAuditingCleaner
{
    /// <summary>
    /// Deletes audit entries older than <paramref name="cutoff"/> for the given
    /// <paramref name="category"/>, processing at most <paramref name="batchSize"/>
    /// rows per round-trip.
    /// </summary>
    /// <returns>The number of entries deleted in this batch.</returns>
    Task<int> PurgeAsync(
        AuditCategory category,
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);
}
