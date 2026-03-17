using Granit.AuditLog.Messages;

namespace Granit.AuditLog.Abstractions;

/// <summary>
/// Internal publisher interface for the interceptor → persistence pipeline.
/// </summary>
/// <remarks>
/// In <see cref="Domain.AuditPersistenceMode.Async"/> mode, writes to a
/// <c>Channel&lt;AuditLogBatch&gt;</c>. In <see cref="Domain.AuditPersistenceMode.Strict"/>
/// mode, persists directly to the <c>AuditLogDbContext</c>.
/// </remarks>
public interface IAuditLogEntryPublisher
{
    /// <summary>
    /// Publishes a captured audit batch for persistence.
    /// </summary>
    ValueTask PublishAsync(AuditLogBatch batch, CancellationToken cancellationToken = default);
}
