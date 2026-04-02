using Granit.Auditing.Domain;

namespace Granit.Auditing;

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

    /// <summary>
    /// Pseudonymizes all audit entries belonging to a specific user (GDPR Art. 17).
    /// Replaces <c>UserId</c> with a SHA-256 hash, <c>UserName</c> with
    /// <c>"[pseudonymized]"</c>, and nullifies <c>IpAddress</c> and <c>UserAgent</c>.
    /// </summary>
    /// <remarks>
    /// Audit trail integrity is preserved (ISO 27001 A.12.4): entries are not deleted,
    /// only personal data fields are replaced. The hashed <c>UserId</c> allows
    /// correlation without re-identification.
    /// </remarks>
    /// <param name="userId">The original user identifier to pseudonymize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of audit entries pseudonymized.</returns>
    Task<int> PseudonymizeByUserAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
