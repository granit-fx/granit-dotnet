namespace Granit.Metering.Recompute;

/// <summary>Outcome of an on-demand recompute.</summary>
/// <param name="MeterDefinitionId">Meter that was recomputed.</param>
/// <param name="WindowStart">Inclusive lower bound of the recomputed window.</param>
/// <param name="WindowEnd">Exclusive upper bound of the recomputed window.</param>
/// <param name="EventsScanned">Total raw <see cref="Domain.MeterEvent"/> rows considered.</param>
/// <param name="AggregatesRebuilt">
/// Total <see cref="Domain.UsageAggregate"/> rows written (created or updated). Equals
/// the number of distinct hourly buckets in the window that received at least one event.
/// </param>
/// <param name="DurationMilliseconds">End-to-end wall-clock duration of the operation.</param>
public sealed record UsageRecomputeResult(
    Guid MeterDefinitionId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    int EventsScanned,
    int AggregatesRebuilt,
    long DurationMilliseconds);
