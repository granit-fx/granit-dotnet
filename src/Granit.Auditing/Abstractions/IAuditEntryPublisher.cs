using Granit.Auditing.Messages;

namespace Granit.Auditing.Abstractions;

/// <summary>
/// Internal publisher interface for the interceptor → persistence pipeline.
/// </summary>
/// <remarks>
/// In <see cref="Domain.AuditPersistenceMode.Async"/> mode, writes to a
/// <c>Channel&lt;AuditingBatch&gt;</c>. In <see cref="Domain.AuditPersistenceMode.Strict"/>
/// mode, persists directly to the <c>AuditingDbContext</c>.
/// </remarks>
public interface IAuditEntryPublisher
{
    /// <summary>
    /// Publishes a captured audit batch for persistence.
    /// </summary>
    ValueTask PublishAsync(AuditingBatch batch, CancellationToken cancellationToken = default);
}
