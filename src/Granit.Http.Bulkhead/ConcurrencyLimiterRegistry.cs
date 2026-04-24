using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Granit.Http.Bulkhead.Options;
using Microsoft.Extensions.Options;

namespace Granit.Http.Bulkhead;

/// <summary>
/// Singleton registry that manages <see cref="ConcurrencyLimiter"/> instances keyed by
/// <c>{policyName}:{tenantSegment}</c>. Each tenant+policy combination gets an independent
/// limiter with its own concurrency and queue limits.
/// </summary>
/// <remarks>
/// <para>
/// Limiters are in-memory per pod/instance. With N pods and <c>PermitLimit=P</c>,
/// a tenant can use up to <c>N×P</c> concurrent operations cluster-wide. This is by design:
/// the bulkhead protects local CPU/memory, not distributed quotas (use <c>Granit.RateLimiting</c> for that).
/// </para>
/// <para>
/// Memory is bounded by <see cref="GranitBulkheadOptions.MaxLimiters"/>. When the cap
/// is reached, an LRU eviction sweep runs synchronously on the next
/// <see cref="AcquireAsync"/> so a new limiter can be installed. This protects
/// against tenant-id spraying: if an attacker forces unique keys faster than
/// the idle-timeout sweeper can clean them up, the registry still cannot grow
/// past the configured bound.
/// </para>
/// </remarks>
public sealed class ConcurrencyLimiterRegistry(
    TimeProvider timeProvider,
    IOptions<GranitBulkheadOptions> options) : IDisposable
{
    private readonly ConcurrentDictionary<string, RegistryEntry> _limiters = new(StringComparer.Ordinal);
    private readonly int _maxLimiters = Math.Max(1, options.Value.MaxLimiters);
    private readonly Lock _evictionLock = new();

    /// <summary>
    /// Acquires a concurrency permit for the given key. Creates the limiter on first access.
    /// </summary>
    public ValueTask<RateLimitLease> AcquireAsync(
        string key,
        int permitLimit,
        int queueLimit,
        CancellationToken cancellationToken)
    {
        // Fast path: limiter already exists — just mark used.
        if (_limiters.TryGetValue(key, out RegistryEntry? existing))
        {
            existing.MarkUsed(timeProvider);
            return existing.Limiter.AcquireAsync(permitCount: 1, cancellationToken);
        }

        // Slow path: create a new limiter, evicting the LRU entry first if the
        // registry is at capacity. The lock bounds concurrent creation so that
        // a burst of unique keys cannot temporarily blow past _maxLimiters.
        RegistryEntry entry;
        lock (_evictionLock)
        {
            if (_limiters.Count >= _maxLimiters && !_limiters.ContainsKey(key))
            {
                EvictLeastRecentlyUsed();
            }

            entry = _limiters.GetOrAdd(key, k => new RegistryEntry(
                new ConcurrencyLimiter(new ConcurrencyLimiterOptions
                {
                    PermitLimit = permitLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = queueLimit,
                }), timeProvider));
        }

        entry.MarkUsed(timeProvider);
        return entry.Limiter.AcquireAsync(permitCount: 1, cancellationToken);
    }

    /// <summary>
    /// Evicts limiters that have been idle longer than <paramref name="idleTimeout"/>.
    /// A limiter is idle when it hasn't been used recently.
    /// </summary>
    public int EvictIdle(TimeSpan idleTimeout)
    {
        int evicted = 0;
        DateTimeOffset threshold = timeProvider.GetUtcNow() - idleTimeout;

        foreach (string key in _limiters.Keys)
        {
            if (!_limiters.TryGetValue(key, out RegistryEntry? entry))
            {
                continue;
            }

            if (entry.LastUsed > threshold)
            {
                continue;
            }

            if (_limiters.TryRemove(key, out RegistryEntry? removed))
            {
                removed.Limiter.Dispose();
                evicted++;
            }
        }

        return evicted;
    }

    /// <summary>Number of active limiters in the registry.</summary>
    internal int Count => _limiters.Count;

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (RegistryEntry entry in _limiters.Values)
        {
            entry.Limiter.Dispose();
        }

        _limiters.Clear();
    }

    private void EvictLeastRecentlyUsed()
    {
        // Linear scan — acceptable at _maxLimiters = 10_000 (microseconds).
        // Upgrading to a real LRU data structure is only warranted if the cap
        // grows by an order of magnitude.
        string? lruKey = null;
        DateTimeOffset lruTimestamp = DateTimeOffset.MaxValue;

        foreach (KeyValuePair<string, RegistryEntry> kvp in _limiters)
        {
            if (kvp.Value.LastUsed < lruTimestamp)
            {
                lruTimestamp = kvp.Value.LastUsed;
                lruKey = kvp.Key;
            }
        }

        if (lruKey is not null && _limiters.TryRemove(lruKey, out RegistryEntry? evicted))
        {
            evicted.Limiter.Dispose();
        }
    }

    private sealed class RegistryEntry(ConcurrencyLimiter limiter, TimeProvider timeProvider)
    {
        public ConcurrencyLimiter Limiter { get; } = limiter;
        public DateTimeOffset LastUsed { get; private set; } = timeProvider.GetUtcNow();

        public void MarkUsed(TimeProvider tp) => LastUsed = tp.GetUtcNow();
    }
}
