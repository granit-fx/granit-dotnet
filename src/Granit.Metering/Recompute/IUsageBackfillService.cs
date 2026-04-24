using Granit.Metering.Domain;

namespace Granit.Metering.Recompute;

/// <summary>
/// Backfills historical <see cref="MeterEvent"/> rows older than the standard
/// ingestion window and triggers an automatic <see cref="IUsageRecomputeService"/>
/// pass over the affected hourly windows so existing aggregates immediately reflect
/// the inserted history.
/// </summary>
/// <remarks>
/// Idempotency is handled by the underlying <see cref="IMeterEventRecorder"/>: events
/// whose <c>(TenantId, IdempotencyKey)</c> already exists are silently ignored at the
/// DB unique-index level. The backfill service then groups the inserted events by
/// meter and recomputes each meter's window in a single transaction (per-meter lock
/// reused from <see cref="IUsageRecomputeService"/>).
/// </remarks>
public interface IUsageBackfillService
{
    /// <summary>Inserts the historical events and recomputes the affected windows.</summary>
    Task<UsageBackfillResult> BackfillAsync(
        IReadOnlyList<MeterEvent> events,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a backfill batch.</summary>
/// <param name="EventsAccepted">Total events the recorder attempted to insert (post-validation).</param>
/// <param name="MetersAffected">Distinct meters that received at least one event.</param>
/// <param name="AggregatesRebuilt">Sum of <c>UsageAggregate</c> rows updated across the per-meter recomputes.</param>
public sealed record UsageBackfillResult(
    int EventsAccepted,
    int MetersAffected,
    int AggregatesRebuilt);
