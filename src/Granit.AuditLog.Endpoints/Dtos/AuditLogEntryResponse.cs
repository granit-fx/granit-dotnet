namespace Granit.AuditLog.Endpoints.Dtos;

/// <summary>
/// Summary response for an audit log entry (list item).
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Timestamp">Operation timestamp (UTC).</param>
/// <param name="UserId">Actor user identifier.</param>
/// <param name="UserName">Actor display name.</param>
/// <param name="Category">Audit log category.</param>
/// <param name="IpAddress">Source IP address.</param>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="CorrelationId">Distributed tracing correlation ID.</param>
/// <param name="EntityChangeCount">Number of entity changes in this entry.</param>
public sealed record AuditLogEntryResponse(
    Guid Id,
    DateTimeOffset Timestamp,
    string UserId,
    string? UserName,
    string Category,
    string? IpAddress,
    Guid? TenantId,
    string? CorrelationId,
    int EntityChangeCount);
