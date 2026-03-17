using Granit.AuditLog.Domain;
using Granit.AuditLog.EntityFrameworkCore.Diagnostics;
using Granit.AuditLog.Options;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.AuditLog.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Background service that periodically purges expired audit log entries
/// based on per-category retention periods.
/// </summary>
internal sealed partial class AuditLogCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AuditLogOptions> optionsMonitor,
    IClock clock,
    AuditLogMetrics metrics,
    ILogger<AuditLogCleanupWorker> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay to let the application stabilize.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeExpiredEntriesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCleanupFailed(ex);
            }

            await Task.Delay(optionsMonitor.CurrentValue.CleanupInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PurgeExpiredEntriesAsync(CancellationToken cancellationToken)
    {
        AuditLogOptions options = optionsMonitor.CurrentValue;

        foreach (AuditLogCategory category in Enum.GetValues<AuditLogCategory>())
        {
            DateTimeOffset cutoff = clock.Now - options.GetRetention(category);
            long totalPurged = 0;
            int batchPurged;

            do
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IDbContextFactory<AuditLogDbContext> dbContextFactory =
                    scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditLogDbContext>>();
                await using AuditLogDbContext dbContext = await dbContextFactory
                    .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                batchPurged = await dbContext.AuditLogEntries
                    .Where(e => e.Category == category && e.Timestamp < cutoff)
                    .Take(options.CleanupBatchSize)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                totalPurged += batchPurged;
            }
            while (batchPurged == options.CleanupBatchSize);

            if (totalPurged > 0)
            {
                metrics.RecordPurged(totalPurged, category.ToString());
                LogEntriesPurged(totalPurged, category.ToString());
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Purged {Count} expired audit log entries for category '{Category}'")]
    private partial void LogEntriesPurged(long count, string category);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Audit log cleanup failed")]
    private partial void LogCleanupFailed(Exception exception);
}
