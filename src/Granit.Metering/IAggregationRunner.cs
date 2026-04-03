namespace Granit.Metering;

/// <summary>
/// Runs aggregation for a single meter definition: reads events past the watermark,
/// computes rollups, and advances the watermark atomically.
/// </summary>
/// <remarks>
/// Implemented by the EF Core package. Consumed by <c>MeteringAggregationHandler</c>.
/// </remarks>
public interface IAggregationRunner
{
    /// <summary>Aggregates pending events for all active meter definitions.</summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}
