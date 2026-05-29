using Granit.Auditing.Messages;

namespace Granit.Auditing;

/// <summary>
/// Persists a single <see cref="AuditingBatch"/> to the underlying store.
/// </summary>
/// <remarks>
/// Implemented by the persistence layer (e.g. EF Core) and consumed by the
/// <c>AuditingPersistenceWorker</c> background service.
/// </remarks>
public interface IAuditBatchPersister
{
    /// <summary>
    /// Maps the batch to a domain entity and persists it.
    /// </summary>
    Task PersistAsync(AuditingBatch batch, CancellationToken cancellationToken = default);
}
