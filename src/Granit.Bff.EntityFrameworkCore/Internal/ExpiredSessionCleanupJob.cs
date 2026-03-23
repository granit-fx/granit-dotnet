using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Bff.EntityFrameworkCore.Internal;

/// <summary>
/// Background job that purges expired BFF sessions from the database.
/// SQL databases have no native TTL — this job runs periodically to reclaim storage.
/// </summary>
internal static partial class ExpiredSessionCleanupHandler
{
    internal static async Task HandleAsync(
        IDbContextFactory<BffDbContext> dbContextFactory,
        IClock clock,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using BffDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        int deleted = await db.Sessions
            .Where(s => s.ExpiresAt <= clock.Now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            LogExpiredSessionsCleaned(logger, deleted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF session cleanup: purged {Count} expired session(s)")]
    private static partial void LogExpiredSessionsCleaned(ILogger logger, int count);
}
