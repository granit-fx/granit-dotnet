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
        if (dataFilter is null)
        {
            Log.DataFilterUnavailable(logger);
            return;
        }

        // Disable tenant filter for the entire aggregation flow: the job runs
        // without tenant context and must enumerate definitions across all tenants,
        // then query each tenant's events with explicit TenantId predicates.
        using IDisposable? _ = dataFilter.Disable<IMultiTenant>();

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

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            Log.AggregationCollisionIgnored(logger, definition.Name, ex);
            return;
        }

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

    /// <summary>
    /// Provider-agnostic duplicate key detection. Mirrors the implementation in
    /// <see cref="EfMeterEventStore"/> to avoid a hard dependency between the two stores.
    /// </summary>
    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        if (ex.InnerException is null)
        {
            return false;
        }

        Type innerType = ex.InnerException.GetType();

        if (innerType.GetProperty("SqlState")?.GetValue(ex.InnerException) is "23505")
        {
            return true; // PostgreSQL unique_violation
        }

        if (innerType.GetProperty("Number")?.GetValue(ex.InnerException) is int and (2601 or 2627))
        {
            return true; // SQL Server unique index / unique constraint
        }

        string? message = ex.InnerException.Message;
        return message?.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug,
            Message = "Aggregation batch completed for meter '{MeterName}': {EventCount} events")]
        public static partial void AggregationBatchCompleted(
            ILogger logger, string meterName, int eventCount);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Aggregation batch for meter '{MeterName}' skipped due to duplicate key collision; a concurrent runner already committed the same period.")]
        public static partial void AggregationCollisionIgnored(
            ILogger logger, string meterName, Exception exception);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Aggregation runner skipped: IDataFilter is not registered. Without the data filter, cross-tenant queries cannot be safely executed.")]
        public static partial void DataFilterUnavailable(ILogger logger);
    }
}
