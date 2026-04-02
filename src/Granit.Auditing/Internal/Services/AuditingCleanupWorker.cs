using System.Diagnostics;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Options;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.Internal.Services;

/// <summary>
/// Background service that periodically purges expired audit log entries
/// based on per-category retention periods.
/// </summary>
internal sealed partial class AuditingCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AuditingOptions> optionsMonitor,
    IClock clock,
    AuditingMetrics metrics,
    ILogger<AuditingCleanupWorker> logger) : BackgroundService
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
        using Activity? activity = AuditingActivitySource.Source.StartActivity(AuditingActivitySource.Cleanup);
        activity?.SetTag("tenant_id", "global");

        AuditingOptions options = optionsMonitor.CurrentValue;

        foreach (AuditCategory category in Enum.GetValues<AuditCategory>())
        {
            DateTimeOffset cutoff = clock.Now - options.GetRetention(category);
            long totalPurged = 0;
            int batchPurged;

            do
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IAuditingCleaner cleaner = scope.ServiceProvider.GetRequiredService<IAuditingCleaner>();

                batchPurged = await cleaner.PurgeAsync(
                    category, cutoff, options.CleanupBatchSize, cancellationToken)
                    .ConfigureAwait(false);

                totalPurged += batchPurged;
            }
            while (batchPurged == options.CleanupBatchSize);

            if (totalPurged > 0)
            {
                metrics.RecordPurged(totalPurged, category.ToString(), tenantId: null);
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
