using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Metering.Domain;

/// <summary>
/// Tracks the last successfully aggregated <see cref="MeterEvent"/> per tenant and meter.
/// </summary>
/// <remarks>
/// <para>
/// The watermark pattern ensures idempotent aggregation without mutating append-only
/// <see cref="MeterEvent"/> records. The aggregation job reads events with
/// <c>Id &gt; LastProcessedEventId</c>, computes rollups, and advances the watermark
/// atomically in the same transaction.
/// </para>
/// <para>
/// If the job crashes mid-batch, the watermark is not advanced — the next run
/// re-processes from the same point. No double-counting, no data loss.
/// </para>
/// </remarks>
public sealed class AggregationWatermark : Entity, IMultiTenant
{
    private AggregationWatermark() { }

    /// <summary>Creates a new watermark for a tenant/meter pair.</summary>
    public static AggregationWatermark Create(
        Guid id,
        Guid meterDefinitionId)
    {
        return new AggregationWatermark
        {
            Id = id,
            MeterDefinitionId = meterDefinitionId,
            LastProcessedEventId = Guid.Empty,
        };
    }

    /// <summary>The meter this watermark tracks.</summary>
    public Guid MeterDefinitionId { get; private set; }

    /// <summary>The ID of the last successfully aggregated event (Guid.Empty if none).</summary>
    public Guid LastProcessedEventId { get; private set; }

    /// <summary>When the last aggregation completed.</summary>
    public DateTimeOffset? LastProcessedAt { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>Advances the watermark after a successful aggregation batch.</summary>
    public void Advance(Guid lastEventId, DateTimeOffset processedAt)
    {
        LastProcessedEventId = lastEventId;
        LastProcessedAt = processedAt;
    }
}
