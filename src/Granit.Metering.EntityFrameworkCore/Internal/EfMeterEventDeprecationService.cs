using Granit.DataFiltering;
using Granit.Domain;
using Granit.Metering.Diagnostics;
using Granit.Metering.Domain;
using Granit.Metering.Recompute;
using Granit.Metering.Recompute.Exceptions;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IMeterEventDeprecationService"/>.
/// </summary>
internal sealed partial class EfMeterEventDeprecationService(
    IDbContextFactory<MeteringDbContext> contextFactory,
    IUsageRecomputeService recomputeService,
    MeteringMetrics metrics,
    IClock clock,
    IDataFilter? dataFilter,
    ILogger<EfMeterEventDeprecationService> logger) : IMeterEventDeprecationService
{
    public async Task<MeterEventDeprecationResult> DeprecateAsync(
        Guid eventId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        // Admins may operate on events from any tenant; the lookup disables the
        // tenant filter, then the recompute step uses the meter's actual TenantId.
        using IDisposable? _ = dataFilter?.Disable<IMultiTenant>();

        DateTimeOffset deprecatedAt;
        Guid meterDefinitionId;

        await using (MeteringDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            MeterEvent? meterEvent = await db.MeterEvents
                .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new MeterEventNotFoundException(eventId);

            deprecatedAt = clock.Now;

            // Throws InvalidOperationException if already deprecated — caller maps to 409.
            meterEvent.Deprecate(reason, deprecatedAt);

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            meterDefinitionId = meterEvent.MeterDefinitionId;

            metrics.RecordEventDeprecated(meterEvent.TenantId?.ToString(), meterEvent.MeterDefinitionId);
            Log.EventDeprecated(logger, meterEvent.Id, meterEvent.MeterDefinitionId, reason);

            // Auto-recompute the affected hourly window so consumers see the corrected
            // aggregate immediately. Uses the recompute service (which acquires the
            // meter-level lock); no additional locking needed here.
            DateTimeOffset windowStart = SnapToHour(meterEvent.Timestamp);
            DateTimeOffset windowEnd = windowStart.AddHours(1);

            UsageRecomputeResult recompute = await recomputeService
                .RecomputeAsync(
                    new UsageRecomputeRequest(meterDefinitionId, windowStart, windowEnd),
                    cancellationToken)
                .ConfigureAwait(false);

            return new MeterEventDeprecationResult(
                eventId, meterDefinitionId, deprecatedAt, recompute);
        }
    }

    private static DateTimeOffset SnapToHour(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Offset);

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Meter event '{EventId}' on meter '{MeterDefinitionId}' soft-deprecated. Reason: {Reason}")]
        public static partial void EventDeprecated(
            ILogger logger, Guid eventId, Guid meterDefinitionId, string reason);
    }
}
