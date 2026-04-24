using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Metering.Domain;

/// <summary>
/// An append-only usage event recorded against a <see cref="MeterDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// MeterEvents are time-series data — they have no state transitions and are never
/// updated. Deduplication is handled at the persistence layer via a unique index on
/// (<see cref="IMultiTenant.TenantId"/>, <see cref="IdempotencyKey"/>).
/// </para>
/// <para>
/// Designed for high-throughput bulk inserts without EF Core change tracking overhead.
/// </para>
/// </remarks>
public sealed class MeterEvent : Entity, IMultiTenant
{
    /// <summary>Hard ceiling for event quantity to prevent aggregation overflow.</summary>
    internal const decimal MaxQuantity = 1_000_000_000m;

    private MeterEvent() { }

    /// <summary>Creates a new meter event.</summary>
    public static MeterEvent Create(
        Guid id,
        Guid meterDefinitionId,
        string idempotencyKey,
        decimal quantity,
        DateTimeOffset timestamp,
        string? metadata = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quantity, MaxQuantity);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new MeterEvent
        {
            Id = id,
            MeterDefinitionId = meterDefinitionId,
            IdempotencyKey = idempotencyKey,
            Quantity = quantity,
            Timestamp = timestamp,
            Metadata = metadata,
        };
    }

    /// <summary>The meter this event belongs to.</summary>
    public Guid MeterDefinitionId { get; private set; }

    /// <summary>Client-provided key for deduplication.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>The measured quantity (e.g., 1 for a single API call, 512 for MB).</summary>
    public decimal Quantity { get; private set; }

    /// <summary>When the usage occurred.</summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>Optional JSON metadata (e.g., endpoint, resource ID).</summary>
    public string? Metadata { get; private set; }

    /// <summary>
    /// When the event was soft-deprecated (UTC), or <c>null</c> when active.
    /// Deprecated events stay on the table for audit (ISO 27001 A.12.4) but are
    /// excluded from aggregation; the per-tenant unique index on
    /// (TenantId, IdempotencyKey) still applies, so a re-ingestion with the
    /// same key is rejected — no resurrection path.
    /// </summary>
    public DateTimeOffset? DeprecatedAt { get; private set; }

    /// <summary>
    /// Free-text reason captured at deprecation time (max 500 chars). Surfaces in
    /// audit logs and admin UIs. <c>null</c> while the event is active.
    /// </summary>
    public string? DeprecationReason { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>Maximum length of <see cref="DeprecationReason"/>.</summary>
    public const int DeprecationReasonMaxLength = 500;

    /// <summary>
    /// Soft-deprecates this event so the aggregator stops counting it. The event
    /// row is preserved for audit. Idempotency is the caller's responsibility —
    /// calling <c>Deprecate</c> on an already-deprecated event throws so the
    /// HTTP layer can return 409.
    /// </summary>
    public void Deprecate(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, DeprecationReasonMaxLength);

        if (DeprecatedAt is not null)
        {
            throw new InvalidOperationException(
                $"Event '{Id}' is already deprecated (at {DeprecatedAt:O}).");
        }

        DeprecatedAt = now;
        DeprecationReason = reason;
    }
}
