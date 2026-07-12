using Granit.Auditing.Domain;
using Granit.Events;

namespace Granit.Auditing.Events;

/// <summary>
/// Published when an audit entry is persisted.
/// Enables real-time security alerting (e.g., SIEM integration for <see cref="AuditCategory.AccessDenied"/> entries).
/// </summary>
/// <remarks>
/// Embedded pipeline: dispatched pre-commit alongside the audit row, so a Wolverine-backed
/// host writes the outbox envelope atomically with the entry. Standalone pipeline and
/// explicit <c>IAuditingWriter</c> writes: dispatched after the isolated save succeeds —
/// the audit row is the source of truth, the event is at-most-once without Wolverine.
/// <see cref="Id"/> is always the persisted <c>AuditEntry.Id</c>.
/// </remarks>
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
