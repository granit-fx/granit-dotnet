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

/// <summary>Snapshot of an <see cref="IHeadlessBrowserPool"/>.</summary>
/// <param name="ActiveBrowsers">Browsers currently held.</param>
/// <param name="ActivePages">Pages currently checked out by callers.</param>
/// <param name="IdlePages">Pages sitting in the pool ready for acquisition.</param>
/// <param name="WaitingAcquisitions">Number of pending acquisitions blocked waiting for a free page.</param>
/// <param name="TotalAcquisitions">Lifetime count of <c>AcquirePageAsync</c> calls served by this pool instance.</param>
public sealed record PoolStats(
    int ActiveBrowsers,
    int ActivePages,
    int IdlePages,
    int WaitingAcquisitions,
    long TotalAcquisitions);
