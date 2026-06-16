namespace Granit.Auditing.Endpoints.Dtos;

/// <summary>
/// Detailed response for a single audit log entry with full change hierarchy.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Timestamp">Operation timestamp (UTC).</param>
/// <param name="UserId">Actor user identifier.</param>
/// <param name="UserName">Actor display name.</param>
/// <param name="Category">Audit log category.</param>
/// <param name="IpAddress">Source IP address.</param>
/// <param name="UserAgent">Client user-agent string captured at the time of the operation.</param>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="CorrelationId">Distributed tracing correlation ID.</param>
/// <param name="EntityChanges">Nested entity and property changes.</param>
public sealed record AuditEntryDetailResponse(
    Guid Id,
    DateTimeOffset Timestamp,
    string UserId,
    string? UserName,
    string Category,
    string? IpAddress,
    string? UserAgent,
    Guid? TenantId,
    string? CorrelationId,
    IReadOnlyList<AuditEntityChangeResponse> EntityChanges);
