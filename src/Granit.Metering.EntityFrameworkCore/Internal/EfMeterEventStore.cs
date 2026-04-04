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
            catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
            {
                Log.DuplicateEventIgnored(logger, TruncateKey(meterEvent.IdempotencyKey));
                db.Entry(meterEvent).State = EntityState.Detached;
            }
        }
    }

    /// <summary>
    /// Provider-agnostic duplicate key detection. Matches error codes when available,
    /// falls back to phrase matching for other providers. Phrases are more specific
    /// than single words to avoid false positives on unrelated constraint errors.
    /// </summary>
    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        if (ex.InnerException is null)
        {
            return false;
        }

        // Check provider-specific error codes via reflection (no hard dependency on Npgsql/SqlClient)
        Type innerType = ex.InnerException.GetType();

        if (innerType.GetProperty("SqlState")?.GetValue(ex.InnerException) is "23505")
        {
            return true; // PostgreSQL unique_violation
        }

        if (innerType.GetProperty("Number")?.GetValue(ex.InnerException) is int num and (2601 or 2627))
        {
            return true; // SQL Server unique index / unique constraint
        }

        // Fallback: phrase matching for SQLite and other providers
        string? message = ex.InnerException.Message;
        return message?.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string TruncateKey(string key) =>
        key.Length <= 12 ? key : string.Concat(key.AsSpan(0, 12), "...");

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Duplicate meter event ignored (idempotency key: {IdempotencyKey})")]
        public static partial void DuplicateEventIgnored(ILogger logger, string idempotencyKey);
    }
}
