using Granit.Bff.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Bff.BackgroundJobs.Internal;

/// <summary>
/// Purges expired BFF sessions from the database.
/// </summary>
internal sealed partial class ExpiredSessionCleanupService(
    IDbContextFactory<BffDbContext> dbContextFactory,
    IClock clock,
    ILogger<ExpiredSessionCleanupService> logger) : IExpiredSessionCleanupService
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await using BffDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        int deleted = await db.Sessions
            .Where(s => s.ExpiresAt <= clock.Now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            Log.ExpiredSessionsCleaned(logger, deleted);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "BFF session cleanup: purged {Count} expired session(s)")]
        public static partial void ExpiredSessionsCleaned(ILogger logger, int count);
    }
}
