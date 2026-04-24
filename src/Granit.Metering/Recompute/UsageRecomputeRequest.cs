namespace Granit.Metering.Recompute;

/// <summary>
/// Domain-level request for an on-demand recompute of pre-computed
/// <see cref="Domain.UsageAggregate"/> rows over a time window.
/// </summary>
/// <param name="MeterDefinitionId">Meter to recompute.</param>
/// <param name="From">Inclusive lower bound of the window (UTC).</param>
/// <param name="To">Exclusive upper bound of the window (UTC).</param>
/// <remarks>
/// The recompute is bounded — the global ingestion watermark is never rewound,
/// so concurrent ingestion past the window is safe. Only past
/// <see cref="Domain.AggregationPeriod.Hourly"/> rows in the window are rewritten.
/// </remarks>
public sealed record UsageRecomputeRequest(
    Guid MeterDefinitionId,
    DateTimeOffset From,
    DateTimeOffset To);
