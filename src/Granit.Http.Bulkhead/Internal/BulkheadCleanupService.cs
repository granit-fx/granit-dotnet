using Granit.Http.Bulkhead.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Http.Bulkhead.Internal;

/// <summary>
/// Background service that periodically evicts idle <see cref="System.Threading.RateLimiting.ConcurrencyLimiter"/>
/// instances from the <see cref="ConcurrencyLimiterRegistry"/> to prevent memory leaks.
/// </summary>
internal sealed class BulkheadCleanupService(
    ConcurrencyLimiterRegistry registry,
    IOptionsMonitor<GranitBulkheadOptions> options,
    TimeProvider timeProvider,
    ILogger<BulkheadCleanupService> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            GranitBulkheadOptions opts = options.CurrentValue;

            try
            {
                await Task.Delay(opts.CleanupInterval, timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            int evicted = registry.EvictIdle(opts.IdleTimeout);

            if (evicted > 0)
            {
                BulkheadLog.LogIdleLimitersEvicted(logger, evicted);
            }
        }
    }
}
