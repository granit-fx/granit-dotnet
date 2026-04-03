using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Metering.Domain;

/// <summary>
/// Pre-computed usage rollup for a meter over a time period.
/// </summary>
/// <remarks>
/// Aggregates are computed by the <c>MeteringAggregationJob</c> background job
/// from raw <see cref="MeterEvent"/> records. The aggregation type (Sum, Max, Count, Last)
/// is determined by the <see cref="MeterDefinition"/>.
/// </remarks>
public sealed class UsageAggregate : Entity, IMultiTenant
{
    private UsageAggregate() { }

    /// <summary>Creates a new usage aggregate.</summary>
    public static UsageAggregate Create(
        Guid id,
        Guid meterDefinitionId,
        AggregationPeriod period,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        decimal aggregatedValue,
        long eventCount)
    {
        return new UsageAggregate
        {
            Id = id,
            MeterDefinitionId = meterDefinitionId,
            Period = period,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            AggregatedValue = aggregatedValue,
            EventCount = eventCount,
        };
    }

    /// <summary>The meter this aggregate belongs to.</summary>
    public Guid MeterDefinitionId { get; private set; }

    /// <summary>The aggregation granularity.</summary>
    public AggregationPeriod Period { get; private set; }

    /// <summary>Start of the aggregation window (inclusive).</summary>
    public DateTimeOffset PeriodStart { get; private set; }

    /// <summary>End of the aggregation window (exclusive).</summary>
    public DateTimeOffset PeriodEnd { get; private set; }

    /// <summary>The computed aggregate value (sum, max, count, or last depending on meter type).</summary>
    public decimal AggregatedValue { get; private set; }

    /// <summary>Number of raw events included in this aggregate.</summary>
    public long EventCount { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>Updates the aggregate with recomputed values (idempotent re-aggregation).</summary>
    internal void Recompute(decimal aggregatedValue, long eventCount)
    {
        AggregatedValue = aggregatedValue;
        EventCount = eventCount;
    }
}
