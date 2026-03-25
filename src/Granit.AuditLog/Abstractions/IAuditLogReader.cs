using Granit.AuditLog.Domain;
using Granit.QueryEngine;

namespace Granit.AuditLog.Abstractions;

/// <summary>
/// Read-side abstraction for querying the audit trail.
/// </summary>
/// <remarks>
/// Defined in the abstractions package so that any module can depend on the
/// reader contract without taking an EF Core dependency. The EF Core
/// implementation lives in <c>Granit.AuditLog.EntityFrameworkCore</c>.
/// </remarks>
public interface IAuditLogReader
{
    /// <summary>
    /// Retrieves a single audit log entry by its unique identifier,
    /// including all nested entity and property changes.
    /// </summary>
    Task<AuditLogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of audit log entries matching the given query filters.
    /// </summary>
    Task<PagedResult<AuditLogEntry>> GetPagedAsync(AuditLogQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the audit trail for a specific entity instance (GDPR SAR support).
    /// </summary>
    Task<PagedResult<AuditLogEntry>> GetByEntityAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default);
}
