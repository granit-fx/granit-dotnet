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

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }
}
