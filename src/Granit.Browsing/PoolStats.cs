namespace Granit.Browsing;

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
