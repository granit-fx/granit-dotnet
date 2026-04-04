using Granit.Metering.Domain;

namespace Granit.Metering.Endpoints.Dtos;

/// <summary>
/// Read-only representation of a <see cref="UsageAggregate"/> for API responses.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="MeterDefinitionId">The meter this aggregate belongs to.</param>
/// <param name="Period">The aggregation granularity.</param>
/// <param name="PeriodStart">Start of the aggregation window (inclusive).</param>
/// <param name="PeriodEnd">End of the aggregation window (exclusive).</param>
/// <param name="AggregatedValue">The computed aggregate value.</param>
/// <param name="EventCount">Number of raw events included.</param>
public sealed record UsageAggregateResponse(
    Guid Id,
    Guid MeterDefinitionId,
    AggregationPeriod Period,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    decimal AggregatedValue,
    long EventCount)
{
    internal static UsageAggregateResponse FromEntity(UsageAggregate aggregate) => new(
        aggregate.Id,
        aggregate.MeterDefinitionId,
        aggregate.Period,
        aggregate.PeriodStart,
        aggregate.PeriodEnd,
        aggregate.AggregatedValue,
        aggregate.EventCount);
}
