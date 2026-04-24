using Granit.Metering.Diagnostics;
using Granit.Metering.Domain;
using Granit.Metering.Recompute;
using Microsoft.Extensions.Logging;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUsageBackfillService"/>.
/// </summary>
internal sealed partial class EfUsageBackfillService(
    IMeterEventRecorder recorder,
    IUsageRecomputeService recomputeService,
    MeteringMetrics metrics,
    ILogger<EfUsageBackfillService> logger) : IUsageBackfillService
{
    public async Task<UsageBackfillResult> BackfillAsync(
        IReadOnlyList<MeterEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            return new UsageBackfillResult(0, 0, 0);
        }

        // The recorder swallows duplicate (TenantId, IdempotencyKey) inserts via the
        // existing dedup pipeline — same path as POST /events, so backfill inherits
        // the per-event idempotency guarantee for free.
        await recorder.RecordBatchAsync(events, cancellationToken).ConfigureAwait(false);

        // Group inserted events by meter to compute the spanning window per meter.
        // The recompute service acquires its own per-(meter, tenant) transactional
        // lock, so we just iterate sequentially — the hot path is the recompute work
        // itself, not the loop overhead.
        IEnumerable<IGrouping<Guid, MeterEvent>> byMeter = events.GroupBy(e => e.MeterDefinitionId);
        int totalAggregatesRebuilt = 0;
        int metersAffected = 0;
        Guid? perBatchTenantId = null;

        foreach (IGrouping<Guid, MeterEvent> meterGroup in byMeter)
        {
            DateTimeOffset windowStart = meterGroup.Min(e => e.Timestamp);
            DateTimeOffset windowEnd = meterGroup.Max(e => e.Timestamp).AddHours(1);

            UsageRecomputeResult result = await recomputeService
                .RecomputeAsync(
                    new UsageRecomputeRequest(meterGroup.Key, windowStart, windowEnd),
                    cancellationToken)
                .ConfigureAwait(false);

            totalAggregatesRebuilt += result.AggregatesRebuilt;
            metersAffected++;

            // Tag the batch metric with the first meter's tenant — backfill batches
            // are always single-tenant in practice (admin scope). If callers ever
            // mix tenants, OpenTelemetry will still aggregate correctly per series.
            perBatchTenantId ??= meterGroup.First().TenantId;
        }

        metrics.RecordBackfill(perBatchTenantId?.ToString(), events.Count);
        Log.BackfillCompleted(logger, events.Count, metersAffected, totalAggregatesRebuilt);

        return new UsageBackfillResult(events.Count, metersAffected, totalAggregatesRebuilt);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Backfill completed: {EventsAccepted} events across {MetersAffected} meters, {AggregatesRebuilt} aggregates rebuilt.")]
        public static partial void BackfillCompleted(
            ILogger logger, int eventsAccepted, int metersAffected, int aggregatesRebuilt);
    }
}
