using Granit.Auditing.Domain;
using Granit.Events;

namespace Granit.Auditing.Events;

/// <summary>
/// Published after an audit entry is successfully persisted (async or strict mode).
/// Enables real-time security alerting (e.g., SIEM integration for <see cref="AuditCategory.AccessDenied"/> entries).
/// </summary>
/// <param name="Id">The unique identifier of the persisted audit entry.</param>
/// <param name="Timestamp">When the audited operation occurred (UTC).</param>
/// <param name="UserId">The actor who triggered the audited operation.</param>
/// <param name="Category">The audit log category.</param>
/// <param name="EntityChangeCount">Number of entity changes in the batch.</param>
/// <param name="TenantId">Tenant identifier (null for global operations).</param>
public sealed record AuditEntryPersistedEto(
    Guid Id,
    DateTimeOffset Timestamp,
    string UserId,
    AuditCategory Category,
    int EntityChangeCount,
    Guid? TenantId) : IIntegrationEvent;
