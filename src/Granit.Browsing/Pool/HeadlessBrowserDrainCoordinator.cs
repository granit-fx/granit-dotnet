using System;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.Browsing.Pool;

/// <summary>
/// Bounds <see cref="IHeadlessBrowserPool.DrainAsync"/> so a misbehaving page handle can
/// never block a graceful shutdown indefinitely.
/// </summary>
internal sealed class HeadlessBrowserDrainCoordinator
{
    private readonly BrowsingMetrics _metrics;
    private readonly ILogger<HeadlessBrowserDrainCoordinator> _logger;

    public HeadlessBrowserDrainCoordinator(
        BrowsingMetrics metrics,
        ILogger<HeadlessBrowserDrainCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);

        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Drains <paramref name="pool"/>, bounded by <paramref name="timeout"/>. On timeout,
    /// emits the <c>granit.browsing.pool.drain_timeout</c> counter, logs a warning, and
    /// force-disposes <paramref name="browser"/> when it implements
    /// <see cref="IAsyncDisposable"/>.
    /// </summary>
    public async Task DrainAsync(
        IHeadlessBrowser browser,
        IHeadlessBrowserPool pool,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(pool);

        try
        {
            await pool.DrainAsync(cancellationToken).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _metrics.RecordPoolDrainTimeout(browser.EngineName);
            _logger.LogWarning(
                "Headless browser pool drain timed out after {Timeout}; forcing disposal of {Engine}.",
                timeout,
                browser.EngineName);

            if (browser is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
