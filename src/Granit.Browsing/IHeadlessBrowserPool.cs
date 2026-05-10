using System.Threading;
using System.Threading.Tasks;

namespace Granit.Browsing;

/// <summary>
/// Operational view of the underlying browser pool — exposed for diagnostics, admin
/// endpoints, and graceful shutdown coordination. Production hosts wire this into a
/// health-check or an admin metrics endpoint.
/// </summary>
public interface IHeadlessBrowserPool
{
    /// <summary>Number of browser instances currently held by the pool (active or idle).</summary>
    int ActiveBrowsers { get; }

    /// <summary>Number of pages currently sitting idle in the pool, ready for acquisition.</summary>
    int IdlePages { get; }

    /// <summary>Returns a snapshot of the pool's state.</summary>
    Task<PoolStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drains the pool — refuses new acquisitions, waits for in-flight pages to be
    /// released, then disposes every browser instance. Used during graceful host
    /// shutdown.
    /// </summary>
    Task DrainAsync(CancellationToken cancellationToken = default);
}
