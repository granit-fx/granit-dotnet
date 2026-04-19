using Granit.Auditing.Domain;

namespace Granit.Auditing.Dtos;

/// <summary>
/// Summary projection of an <see cref="AuditEntry"/> for list/query endpoints.
/// </summary>
/// <remarks>
/// Returned by the query engine when <c>MapGranitQuery&lt;AuditEntry&gt;</c> is mounted
/// with <c>AuditEntryQueryDefinition</c>. <c>UserAgent</c> is deliberately excluded from
/// the list view — it is only exposed via the detail endpoint. <c>EntityChangeCount</c>
/// is computed server-side via a SQL subquery on <c>AuditEntityChange</c>.
/// </remarks>
public sealed record AuditEntrySummary(
    Guid Id,
    DateTimeOffset Timestamp,
    string UserId,
    string? UserName,
    AuditCategory Category,
    string? IpAddress,
    Guid? TenantId,
    string? CorrelationId,
    int EntityChangeCount);
