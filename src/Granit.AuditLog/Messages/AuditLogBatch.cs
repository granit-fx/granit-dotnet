using Granit.AuditLog.Domain;

namespace Granit.AuditLog.Messages;

/// <summary>
/// Captured audit data published from the interceptor to the persistence pipeline.
/// This is a channel message, not an EF entity.
/// </summary>
/// <param name="Timestamp">Operation timestamp (UTC).</param>
/// <param name="UserId">Actor user identifier.</param>
/// <param name="UserName">Actor display name (nullable for pseudonymization).</param>
/// <param name="Category">Audit log category.</param>
/// <param name="IpAddress">Source IP address.</param>
/// <param name="UserAgent">Client User-Agent header.</param>
/// <param name="TenantId">Tenant identifier (null for global operations).</param>
/// <param name="CorrelationId">Distributed tracing correlation ID.</param>
/// <param name="EntityChanges">Captured entity changes.</param>
public sealed record AuditLogBatch(
    DateTimeOffset Timestamp,
    string UserId,
    string? UserName,
    AuditLogCategory Category,
    string? IpAddress,
    string? UserAgent,
    Guid? TenantId,
    string? CorrelationId,
    IReadOnlyList<AuditEntityChangeSnapshot> EntityChanges);
