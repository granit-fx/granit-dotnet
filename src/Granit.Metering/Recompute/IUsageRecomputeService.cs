namespace Granit.Metering.Recompute;

/// <summary>
/// Recomputes pre-computed <see cref="Domain.UsageAggregate"/> rows over a chosen
/// time window. Used after data corrections (deprecated events, backfilled events,
/// formula tweaks) to repair past billing data without touching the forward-looking
/// ingestion watermark.
/// </summary>
/// <remarks>
/// Implementations must guard against concurrent execution with the hourly aggregation
/// job by acquiring a database-side lock on <c>(MeterDefinitionId, TenantId)</c> within
/// the same transaction that rewrites the aggregates. The lock auto-releases on
/// COMMIT/ROLLBACK; both the recompute service and the aggregation job acquire the
/// same lock, so they serialize naturally without affecting raw ingestion.
/// </remarks>
public interface IUsageRecomputeService
{
    /// <summary>Recomputes the meter over the requested window.</summary>
    /// <param name="request">Meter id and inclusive/exclusive bounds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of events scanned and aggregates rebuilt.</returns>
    /// <exception cref="UsageRecomputeRejectedException">
    /// The meter is unknown, archived, or the window is invalid.
    /// </exception>
    Task<UsageRecomputeResult> RecomputeAsync(
        UsageRecomputeRequest request,
        CancellationToken cancellationToken = default);
}
