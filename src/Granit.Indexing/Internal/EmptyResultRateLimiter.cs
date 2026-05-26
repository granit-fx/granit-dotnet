using System.Collections.Concurrent;
using Granit.Indexing.Diagnostics;
using Granit.Indexing.Options;
using Granit.MultiTenancy;

namespace Granit.Indexing.Internal;

/// <summary>
/// In-memory sliding-window <see cref="IEmptyResultRateLimiter"/>. Per-principal bucket
/// of timestamps; the limiter trims expired entries on every call so memory grows only
/// with active distinct principals over the trailing minute.
/// </summary>
/// <remarks>
/// Uses <see cref="TimeProvider"/> rather than <see cref="DateTime.UtcNow"/> so tests
/// can freeze and advance the clock deterministically.
/// </remarks>
internal sealed class EmptyResultRateLimiter : IEmptyResultRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _timeProvider;
    private readonly GranitIndexingOptions _options;
    private readonly IndexingMetrics _metrics;
    private readonly ICurrentTenant _currentTenant;
    private readonly ConcurrentDictionary<string, PrincipalWindow> _principals = new(StringComparer.Ordinal);

    public EmptyResultRateLimiter(
        TimeProvider timeProvider,
        GranitIndexingOptions options,
        IndexingMetrics metrics,
        ICurrentTenant currentTenant)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(currentTenant);
        _timeProvider = timeProvider;
        _options = options;
        _metrics = metrics;
        _currentTenant = currentTenant;
    }

    public bool RecordEmptyResultAndShouldThrottle(string principalIdentifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(principalIdentifier);

        long nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        long cutoffTicks = nowTicks - Window.Ticks;

        PrincipalWindow window = _principals.GetOrAdd(principalIdentifier, static _ => new PrincipalWindow());

        bool throttle;
        lock (window.Lock)
        {
            // Trim expired entries.
            while (window.Timestamps.Count > 0 && window.Timestamps.Peek() < cutoffTicks)
            {
                window.Timestamps.Dequeue();
            }

            window.Timestamps.Enqueue(nowTicks);
            throttle = window.Timestamps.Count > _options.MaxEmptyResultQueriesPerPrincipalPerMinute;
        }

        if (throttle)
        {
            _metrics.RecordEmptyResultThrottled(_currentTenant.Id?.ToString());
        }

        return throttle;
    }

    private sealed class PrincipalWindow
    {
        internal readonly Lock Lock = new();
        internal readonly Queue<long> Timestamps = new();
    }
}
