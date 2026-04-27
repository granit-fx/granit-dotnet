using System.Text.Json;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Guids;
using Granit.Metering.Diagnostics;
using Granit.Metering.Domain;
using Granit.Persistence.EntityFrameworkCore.ExceptionHandling;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

        // Mutually-exclude with on-demand recompute on the same (meter, tenant) — the
        // recompute service acquires the same lock. The lock auto-releases at COMMIT/ROLLBACK
        // so there is no leak risk on transient failure.
        await using IDbContextTransaction tx = await db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await MeteringConcurrencyLock
            .AcquireAsync(db, definition.Id, definition.TenantId, cancellationToken)
            .ConfigureAwait(false);

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
                && e.Id.CompareTo(watermark.LastProcessedEventId) > 0
                && e.DeprecatedAt == null)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (events.Count == 0)
        {
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        DateTimeOffset now = clock.Now;
        DateTimeOffset periodStart = new(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
        DateTimeOffset periodEnd = periodStart.AddHours(1);

        decimal aggregatedValue = Aggregate(definition, events);

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
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (DbUpdateExceptionHelper.IsDuplicateKeyException(ex))
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            Log.AggregationCollisionIgnored(logger, definition.Name, ex);
            return;
        }

        metrics.RecordAggregation(
            definition.TenantId?.ToString(),
            definition.Id,
            events.Count);

        Log.AggregationBatchCompleted(logger, definition.Name, events.Count);
    }

    internal static decimal Aggregate(MeterDefinition definition, List<MeterEvent> events) =>
        definition.AggregationType switch
        {
            AggregationType.Sum => events.Sum(e => e.Quantity),
            AggregationType.Max => events.Max(e => e.Quantity),
            AggregationType.Count => events.Count,
            AggregationType.Last => events[^1].Quantity,
            AggregationType.CountDistinct => CountDistinctValues(events, definition.DistinctProperty),
            _ => events.Sum(e => e.Quantity),
        };

    /// <summary>
    /// Counts distinct, non-null string values of <paramref name="property"/> extracted
    /// from each event's <c>Metadata</c> JSON. Events with null/empty/invalid metadata,
    /// or missing the property, are excluded entirely (never bucketed as a synthetic
    /// <c>null</c>). Numbers and booleans are stringified ordinally so
    /// <c>{"id": 1}</c> and <c>{"id": "1"}</c> hash to the same bucket.
    /// </summary>
    private static decimal CountDistinctValues(List<MeterEvent> events, string? property)
    {
        if (string.IsNullOrWhiteSpace(property))
        {
            return 0m;
        }

        HashSet<string> distinct = new(StringComparer.Ordinal);

        IEnumerable<string> values = events
            .Select(ev => ev.Metadata)
            .Where(metadata => !string.IsNullOrWhiteSpace(metadata))
            .Select(metadata => TryExtractStringValue(metadata!, property))
            .Where(value => value is not null)
            .Select(value => value!);
        foreach (string value in values)
        {
            distinct.Add(value);
        }

        return distinct.Count;
    }

    private static string? TryExtractStringValue(string metadataJson, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!doc.RootElement.TryGetProperty(property, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
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
