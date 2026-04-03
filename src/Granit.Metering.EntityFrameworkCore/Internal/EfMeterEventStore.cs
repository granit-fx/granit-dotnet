using Granit.Metering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IMeterEventRecorder"/>.
/// Uses insert-first dedup: attempts insert, catches unique constraint violation silently.
/// </summary>
internal sealed partial class EfMeterEventStore(
    IDbContextFactory<MeteringDbContext> contextFactory,
    ILogger<EfMeterEventStore> logger) : IMeterEventRecorder
{
    public async Task RecordAsync(MeterEvent meterEvent, CancellationToken cancellationToken = default)
    {
        await using MeteringDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        db.MeterEvents.Add(meterEvent);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            Log.DuplicateEventIgnored(logger, meterEvent.IdempotencyKey);
        }
    }

    public async Task RecordBatchAsync(IReadOnlyList<MeterEvent> events, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using MeteringDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (MeterEvent meterEvent in events)
        {
            db.MeterEvents.Add(meterEvent);

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
            {
                Log.DuplicateEventIgnored(logger, meterEvent.IdempotencyKey);
                db.Entry(meterEvent).State = EntityState.Detached;
            }
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Duplicate meter event ignored (idempotency key: {IdempotencyKey})")]
        public static partial void DuplicateEventIgnored(ILogger logger, string idempotencyKey);
    }
}
