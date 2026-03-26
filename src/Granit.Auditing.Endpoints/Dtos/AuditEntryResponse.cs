namespace Granit.Auditing.Endpoints.Dtos;

/// <summary>
/// Summary response for an audit log entry (list item).
/// </summary>
/// <remarks>
/// IpAddress and UserAgent are intentionally omitted from the summary response
/// (GDPR Art. 25 — data minimization). Use the detail endpoint to access them.
/// </remarks>
/// <param name="Id">Unique identifier.</param>
/// <param name="Timestamp">Operation timestamp (UTC).</param>
/// <param name="UserId">Actor user identifier.</param>
/// <param name="UserName">Actor display name.</param>
/// <param name="Category">Audit log category.</param>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="CorrelationId">Distributed tracing correlation ID.</param>
/// <param name="EntityChangeCount">Number of entity changes in this entry.</param>
public sealed record AuditEntryResponse(
    Guid Id,
    DateTimeOffset Timestamp,
    string UserId,
    string? UserName,
    string Category,
    Guid? TenantId,
    string? CorrelationId,
    int EntityChangeCount);
