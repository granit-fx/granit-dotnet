using Granit.DataFiltering;
using Granit.Domain;
using Granit.Guids;
using Granit.Metering.Diagnostics;
using Granit.Metering.Domain;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IAggregationRunner"/>.
/// Reads events past the watermark, computes rollups per meter definition,
/// upserts <see cref="UsageAggregate"/> records, and advances the watermark atomically.
/// </summary>
internal sealed partial class EfAggregationRunner(
    IDbContextFactory<MeteringDbContext> contextFactory,
    IMeterDefinitionReader definitionReader,
    MeteringMetrics metrics,
    IClock clock,
    IGuidGenerator guidGenerator,
    IDataFilter? dataFilter,
    ILogger<EfAggregationRunner> logger) : IAggregationRunner
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Disable tenant filter for the entire aggregation flow: the job runs
        // without tenant context and must enumerate definitions across all tenants,
        // then query each tenant's events with explicit TenantId predicates.
        using IDisposable? _ = dataFilter?.Disable<IMultiTenant>();

        IReadOnlyList<MeterDefinition> definitions = await definitionReader
            .GetActiveAsync(cancellationToken).ConfigureAwait(false);

        foreach (MeterDefinition definition in definitions)
        {
            await AggregateForDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AggregateForDefinitionAsync(
        MeterDefinition definition,
        CancellationToken cancellationToken)
    {
        await using MeteringDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        AggregationWatermark? watermark = await db.AggregationWatermarks
            .FirstOrDefaultAsync(
                w => w.MeterDefinitionId == definition.Id
                    && w.TenantId == definition.TenantId,
                cancellationToken)
            .ConfigureAwait(false);

        if (watermark is null)
        {
            watermark = AggregationWatermark.Create(guidGenerator.Create(), definition.Id);
            db.AggregationWatermarks.Add(watermark);
        }

        List<MeterEvent> events = await db.MeterEvents
            .Where(e => e.MeterDefinitionId == definition.Id
                && e.TenantId == definition.TenantId
                && e.Id.CompareTo(watermark.LastProcessedEventId) > 0)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (events.Count == 0)
        {
            return;
        }

        DateTimeOffset now = clock.Now;
        DateTimeOffset periodStart = new(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
        DateTimeOffset periodEnd = periodStart.AddHours(1);

        decimal aggregatedValue = Aggregate(definition.AggregationType, events);

        UsageAggregate? existing = await db.UsageAggregates
            .FirstOrDefaultAsync(
                a => a.MeterDefinitionId == definition.Id
                    && a.TenantId == definition.TenantId
                    && a.Period == AggregationPeriod.Hourly
                    && a.PeriodStart == periodStart,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.Recompute(aggregatedValue, events.Count);
        }
        else
        {
            var aggregate = UsageAggregate.Create(
                guidGenerator.Create(),
                definition.Id,
                AggregationPeriod.Hourly,
                periodStart,
                periodEnd,
                aggregatedValue,
                events.Count);
            db.UsageAggregates.Add(aggregate);
        }

        watermark.Advance(events[^1].Id, now);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        metrics.RecordAggregation(
            definition.TenantId?.ToString(),
            definition.Id,
            events.Count);

        Log.AggregationBatchCompleted(logger, definition.Name, events.Count);
    }

    private static decimal Aggregate(AggregationType type, List<MeterEvent> events) =>
        type switch
        {
            AggregationType.Sum => events.Sum(e => e.Quantity),
            AggregationType.Max => events.Max(e => e.Quantity),
            AggregationType.Count => events.Count,
            AggregationType.Last => events[^1].Quantity,
            _ => events.Sum(e => e.Quantity),
        };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug,
            Message = "Aggregation batch completed for meter '{MeterName}': {EventCount} events")]
        public static partial void AggregationBatchCompleted(
            ILogger logger, string meterName, int eventCount);
    }
}
