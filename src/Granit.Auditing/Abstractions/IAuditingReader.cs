using Granit.Auditing.Domain;
using Granit.QueryEngine;

namespace Granit.Auditing.Abstractions;

/// <summary>
/// Read-side abstraction for querying the audit trail.
/// </summary>
/// <remarks>
/// Defined in the abstractions package so that any module can depend on the
/// reader contract without taking an EF Core dependency. The EF Core
/// implementation lives in <c>Granit.Auditing.EntityFrameworkCore</c>.
/// </remarks>
public interface IAuditingReader
{
    /// <summary>
    /// Retrieves a single audit log entry by its unique identifier,
    /// including all nested entity and property changes.
    /// </summary>
    Task<AuditEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of audit log entries matching the given query filters.
    /// </summary>
    Task<PagedResult<AuditEntry>> GetPagedAsync(AuditingQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the audit trail for a specific entity instance (GDPR SAR support).
    /// </summary>
    Task<PagedResult<AuditEntry>> GetByEntityAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default);
}
