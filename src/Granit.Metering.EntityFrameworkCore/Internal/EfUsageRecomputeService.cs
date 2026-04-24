using System.Diagnostics;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Guids;
using Granit.Metering.Diagnostics;
using Granit.Metering.Domain;
using Granit.Metering.Recompute;
using Granit.Persistence.EntityFrameworkCore.ExceptionHandling;
using Granit.Timing;
using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUsageRecomputeService"/>.
/// </summary>
internal sealed partial class EfUsageRecomputeService(
    IDbContextFactory<MeteringDbContext> contextFactory,
    IMeterDefinitionReader definitionReader,
    MeteringMetrics metrics,
    IClock clock,
    IGuidGenerator guidGenerator,
    IDataFilter? dataFilter,
    ILogger<EfUsageRecomputeService> logger) : IUsageRecomputeService
{
    public async Task<UsageRecomputeResult> RecomputeAsync(
        UsageRecomputeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.From >= request.To)
        {
            throw new UsageRecomputeRejectedException(
                "Granit:Metering:RecomputeWindowInvalid",
                $"Recompute window is empty or inverted: from={request.From:O} to={request.To:O}.");
        }

        if (request.From > clock.Now)
        {
            throw new UsageRecomputeRejectedException(
                "Granit:Metering:RecomputeWindowInFuture",
                $"Recompute window lower bound {request.From:O} is in the future (now={clock.Now:O}). "
                + "The upper bound may extend past now (no events past now will be considered).");
        }

        // Definitions are loaded with the tenant filter disabled because the recompute
        // can be invoked by host admins acting on behalf of a tenant; the lock + writes
        // below explicitly scope to the meter's own TenantId.
        using IDisposable? _ = dataFilter?.Disable<IMultiTenant>();

        MeterDefinition? definition = await definitionReader
            .GetByIdAsync(Domain.ValueObjects.MeterDefinitionId.Create(request.MeterDefinitionId), cancellationToken)
            .ConfigureAwait(false);

        if (definition is null)
        {
            throw new UsageRecomputeRejectedException(
                "Granit:Metering:RecomputeMeterNotFound",
                $"Meter '{request.MeterDefinitionId}' not found.");
        }

        if (definition.LifecycleStatus == WorkflowLifecycleStatus.Archived)
        {
            throw new UsageRecomputeRejectedException(
                "Granit:Metering:RecomputeMeterArchived",
                $"Meter '{request.MeterDefinitionId}' is Archived; recompute is not allowed on archived meters.");
        }

        long startTimestamp = Stopwatch.GetTimestamp();

        await using MeteringDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using IDbContextTransaction tx = await db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await MeteringConcurrencyLock
            .AcquireAsync(db, definition.Id, definition.TenantId, cancellationToken)
            .ConfigureAwait(false);

        // Snap window edges to hourly buckets so partial hours are fully rebuilt.
        DateTimeOffset windowStart = SnapToHour(request.From);
        DateTimeOffset windowEnd = SnapToHour(request.To);
        if (windowEnd <= windowStart)
        {
            windowEnd = windowStart.AddHours(1);
        }

        // Pull every event in the window (newest aggregator pass would have included
        // these); group by hourly bucket and feed each group through the same Aggregate
        // function used by the hourly job, so behavior stays consistent.
        List<MeterEvent> events = await db.MeterEvents
            .Where(e => e.MeterDefinitionId == definition.Id
                && e.TenantId == definition.TenantId
                && e.Timestamp >= windowStart
                && e.Timestamp < windowEnd
                && e.DeprecatedAt == null)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Existing aggregates in the same window — we'll either Recompute() or Remove() them.
        List<UsageAggregate> existingAggregates = await db.UsageAggregates
            .Where(a => a.MeterDefinitionId == definition.Id
                && a.TenantId == definition.TenantId
                && a.Period == AggregationPeriod.Hourly
                && a.PeriodStart >= windowStart
                && a.PeriodStart < windowEnd)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingByStart = existingAggregates.ToDictionary(a => a.PeriodStart);

        IEnumerable<IGrouping<DateTimeOffset, MeterEvent>> bucketed = events.GroupBy(e => SnapToHour(e.Timestamp));

        int aggregatesRebuilt = 0;
        HashSet<DateTimeOffset> bucketsTouched = [];

        foreach (IGrouping<DateTimeOffset, MeterEvent> bucket in bucketed)
        {
            DateTimeOffset bucketStart = bucket.Key;
            DateTimeOffset bucketEnd = bucketStart.AddHours(1);
            decimal aggregatedValue = EfAggregationRunner.Aggregate(definition, [.. bucket]);
            int eventCount = bucket.Count();
            bucketsTouched.Add(bucketStart);

            if (existingByStart.TryGetValue(bucketStart, out UsageAggregate? existing))
            {
                existing.Recompute(aggregatedValue, eventCount);
            }
            else
            {
                db.UsageAggregates.Add(UsageAggregate.Create(
                    guidGenerator.Create(),
                    definition.Id,
                    AggregationPeriod.Hourly,
                    bucketStart,
                    bucketEnd,
                    aggregatedValue,
                    eventCount));
            }

            aggregatesRebuilt++;
        }

        // Buckets that previously had aggregates but no longer have any events
        // (events were deprecated/deleted) must be removed — leaving stale rows
        // would silently overcharge customers.
        foreach (UsageAggregate stale in existingAggregates
            .Where(a => !bucketsTouched.Contains(a.PeriodStart)))
        {
            db.UsageAggregates.Remove(stale);
            aggregatesRebuilt++;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (DbUpdateExceptionHelper.IsDuplicateKeyException(ex))
        {
            // Concurrent recompute or aggregator commit raced past our lock acquisition;
            // because both sides take the same lock this should not happen — log and
            // bubble out, the caller can retry.
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            Log.RecomputeCollision(logger, definition.Name, ex);
            throw;
        }

        long elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        metrics.RecordRecompute(
            definition.TenantId?.ToString(),
            definition.Id,
            aggregatesRebuilt);

        Log.RecomputeCompleted(
            logger,
            definition.Name,
            windowStart,
            windowEnd,
            events.Count,
            aggregatesRebuilt,
            elapsedMs);

        return new UsageRecomputeResult(
            definition.Id,
            windowStart,
            windowEnd,
            events.Count,
            aggregatesRebuilt,
            elapsedMs);
    }

    private static DateTimeOffset SnapToHour(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Offset);

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Recompute completed for meter '{MeterName}' over [{WindowStart:O}, {WindowEnd:O}): {EventsScanned} events, {AggregatesRebuilt} aggregates rebuilt in {ElapsedMs}ms")]
        public static partial void RecomputeCompleted(
            ILogger logger,
            string meterName,
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            int eventsScanned,
            int aggregatesRebuilt,
            long elapsedMs);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Recompute for meter '{MeterName}' aborted on duplicate-key collision; retry safely.")]
        public static partial void RecomputeCollision(
            ILogger logger, string meterName, Exception exception);
    }
}
