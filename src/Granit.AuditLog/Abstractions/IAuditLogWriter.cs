using Granit.AuditLog.Domain;

namespace Granit.AuditLog.Abstractions;

/// <summary>
/// Write-side abstraction for persisting explicit audit log entries.
/// </summary>
/// <remarks>
/// Use for events not captured by the EF Core interceptor: login attempts,
/// authorization failures, configuration changes, data access reads.
/// The interceptor handles entity CRUD automatically.
/// </remarks>
public interface IAuditLogWriter
{
    /// <summary>
    /// Persists a single audit log entry directly (synchronous write).
    /// </summary>
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}
