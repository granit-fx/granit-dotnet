using System.Diagnostics;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Options;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.BackgroundJobs.Internal;

/// <summary>
/// Purges expired audit log entries per category retention, in batches. A fresh
/// DI scope is opened per batch so the EF Core change tracker never accumulates
/// across a large purge.
/// </summary>
internal sealed partial class AuditRetentionCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditingOptions> options,
    IClock clock,
    AuditingMetrics metrics,
    ILogger<AuditRetentionCleanupService> logger) : IAuditRetentionCleanupService
{
    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        using Activity? activity = AuditingActivitySource.Source.StartActivity(AuditingActivitySource.Cleanup);
        activity?.SetTag("tenant_id", "global");

        AuditingOptions opts = options.Value;
        LogCleanupStarted();

        foreach (AuditCategory category in Enum.GetValues<AuditCategory>())
        {
            DateTimeOffset cutoff = clock.Now - opts.GetRetention(category);
            long totalPurged = 0;
            int batchPurged;

            do
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IAuditingCleaner cleaner = scope.ServiceProvider.GetRequiredService<IAuditingCleaner>();

                batchPurged = await cleaner.PurgeAsync(
                    category, cutoff, opts.CleanupBatchSize, cancellationToken)
                    .ConfigureAwait(false);

                totalPurged += batchPurged;
            }
            while (batchPurged == opts.CleanupBatchSize);

            if (totalPurged > 0)
            {
                metrics.RecordPurged(totalPurged, category.ToString(), tenantId: null);
            }

            LogCategoryPurged(category.ToString(), totalPurged, cutoff);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Audit log cleanup started")]
    private partial void LogCleanupStarted();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Audit log cleanup '{Category}': purged {Count} entries (cutoff: {Cutoff})")]
    private partial void LogCategoryPurged(string category, long count, DateTimeOffset cutoff);
}
