using Granit.Metering.Domain;
using Granit.Persistence.EntityFrameworkCore.ExceptionHandling;
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
        catch (DbUpdateException ex) when (DbUpdateExceptionHelper.IsDuplicateKeyException(ex))
        {
            Log.DuplicateEventIgnored(logger, TruncateKey(meterEvent.IdempotencyKey));
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
            catch (DbUpdateException ex) when (DbUpdateExceptionHelper.IsDuplicateKeyException(ex))
            {
                Log.DuplicateEventIgnored(logger, TruncateKey(meterEvent.IdempotencyKey));
                db.Entry(meterEvent).State = EntityState.Detached;
            }
        }
    }

    private static string TruncateKey(string key) =>
        key.Length <= 12 ? key : string.Concat(key.AsSpan(0, 12), "...");

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Duplicate meter event ignored (idempotency key: {IdempotencyKey})")]
        public static partial void DuplicateEventIgnored(ILogger logger, string idempotencyKey);
    }
}
