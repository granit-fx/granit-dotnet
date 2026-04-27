using Granit.Mergeable.EntityFrameworkCore;
using Granit.Mergeable.EntityFrameworkCore.Internal;
using Granit.Mergeable.EntityFrameworkCore.Options;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Mergeable.BackgroundJobs.Services;

/// <summary>
/// Sweeps the <c>granit.merge_idempotency</c> cache by deleting rows older than the
/// configured <see cref="MergeableOptions.IdempotencyRetention"/> window. Runs as a single
/// bulk SQL <c>ExecuteDeleteAsync</c> — no rows are loaded into the change tracker, no
/// interceptor side-effects fire (the cache table is bookkeeping, not domain data; it has
/// no audit trail, soft delete, or events).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a sweeper rather than a TTL column.</b> Postgres has no native row-TTL; the
/// cleanest portable option is a recurring delete keyed on <c>CreatedAt</c>. The job runs
/// hourly so retention bounds raised by ops take effect within the hour without restart.
/// </para>
/// <para>
/// <b>Failure semantics.</b> A delete failure logs and returns — the next run retries the
/// sweep. Idempotent by construction.
/// </para>
/// </remarks>
internal sealed partial class MergeIdempotencyCleanupService(
    IDbContextFactory<MergeableDbContext> contextFactory,
    IOptions<MergeableOptions> options,
    IClock clock,
    ILogger<MergeIdempotencyCleanupService> logger) : IMergeIdempotencySweeper
{
    private readonly MergeableOptions _options = options.Value;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset cutoff = clock.Now - _options.IdempotencyRetention;

        await using MergeableDbContext db =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        int deleted = await db.MergeIdempotencyEntries
            .Where(e => e.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        LogSweepCompleted(logger, deleted, cutoff);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Mergeable idempotency cache sweep removed {DeletedCount} expired rows (cutoff: {Cutoff:O}).")]
    private static partial void LogSweepCompleted(ILogger logger, int deletedCount, DateTimeOffset cutoff);
}
