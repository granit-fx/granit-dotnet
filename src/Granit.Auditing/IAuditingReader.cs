using Granit.Auditing.Domain;
using Granit.QueryEngine;

namespace Granit.Auditing;

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
    /// Retrieves the audit trail for a specific entity instance (GDPR SAR support).
    /// </summary>
    Task<PagedResult<AuditEntry>> GetByEntityAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all audit log entries matching a distributed tracing correlation ID.
    /// </summary>
    Task<List<AuditEntry>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit log entries for a given user (GDPR Art. 15 — right of access), ordered
    /// from most recent. <paramref name="limit"/> caps the result size so the archive assembly
    /// stays within <c>GranitPrivacyOptions.ExportMaxSizeMb</c>.
    /// </summary>
    /// <param name="userId">User identifier recorded on <see cref="AuditEntry.UserId"/>.</param>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<AuditEntry>> GetByUserAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken = default);
}
