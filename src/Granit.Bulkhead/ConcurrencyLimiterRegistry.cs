using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Granit.Bulkhead.Diagnostics;
using Granit.Bulkhead.Options;
using Microsoft.Extensions.Options;

namespace Granit.Bulkhead;

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
/// <para>
/// <see cref="System.Threading.RateLimiting.PartitionedRateLimiter"/> was evaluated as a
/// replacement (BCL primitive, same per-partition lifecycle) and rejected: it exposes no
/// partition-count cap, so the <see cref="GranitBulkheadOptions.MaxLimiters"/> anti-spraying
/// bound (k8s hardening #2510) and the outstanding-lease-safe manual eviction cannot be
/// expressed on top of it without re-introducing this registry around it.
/// </para>
/// </remarks>
public sealed class ConcurrencyLimiterRegistry(
    TimeProvider timeProvider,
    IOptions<GranitBulkheadOptions> options,
    BulkheadMetrics metrics) : IDisposable
{
    private readonly ConcurrentDictionary<string, RegistryEntry> _limiters = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<RegistryEntry> _draining = new();
    private readonly int _maxLimiters = Math.Max(1, options.Value.MaxLimiters);
    private readonly Lock _evictionLock = new();

    /// <summary>
    /// Acquires a concurrency permit for the given key. Creates the limiter on first access.
    /// </summary>
    public async ValueTask<RateLimitLease> AcquireAsync(
        string key,
        int permitLimit,
        int queueLimit,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            // Fast path: limiter already exists — just mark used.
            if (_limiters.TryGetValue(key, out RegistryEntry? existing))
            {
                existing.MarkUsed(timeProvider);
                try
                {
                    return await existing.Limiter.AcquireAsync(permitCount: 1, cancellationToken).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    // Lost the race against eviction: the reference was grabbed just
                    // before the sweeper retired this entry. Drop the stale mapping
                    // (if still present) and retry — the slow path installs a fresh
                    // limiter. Self-healing beats locking the hot path.
                    _limiters.TryRemove(new KeyValuePair<string, RegistryEntry>(key, existing));
                    continue;
                }
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
                    }), permitLimit, timeProvider));
            }

            entry.MarkUsed(timeProvider);
            try
            {
                return await entry.Limiter.AcquireAsync(permitCount: 1, cancellationToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                _limiters.TryRemove(new KeyValuePair<string, RegistryEntry>(key, entry));
            }
        }
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

            // Idle by timestamp is not the same as inactive: a long-running operation acquired
            // before the threshold may still hold a permit. Disposing the limiter here would
            // throw ObjectDisposedException on the eventual lease release and skew the
            // active-counter. Skip the entry; the next sweep will retry once it has truly drained.
            if (entry.HasOutstandingWork())
            {
                continue;
            }

            if (_limiters.TryRemove(key, out RegistryEntry? removed))
            {
                // Two-phase retirement: removing the mapping makes the entry invisible
                // to new acquires, but a fast-path caller may have grabbed the reference
                // a moment earlier. Disposal is deferred to the NEXT sweep (DrainRetired)
                // and only happens once the limiter has no outstanding work — closing
                // the remove/dispose vs. acquire race the contention tests exercise.
                _draining.Enqueue(removed);
                evicted++;
            }
        }

        DrainRetired();

        metrics.RecordEvicted("idle", evicted);
        return evicted;
    }

    /// <summary>
    /// Disposes retired entries whose limiter has fully drained. Entries still carrying
    /// permits or queued waiters are re-enqueued for a later sweep. Disposal is not
    /// counted as an eviction — the entry left the registry when it was retired.
    /// </summary>
    private void DrainRetired()
    {
        int pending = _draining.Count;
        for (int i = 0; i < pending && _draining.TryDequeue(out RegistryEntry? retired); i++)
        {
            if (retired.HasOutstandingWork())
            {
                _draining.Enqueue(retired);
                continue;
            }

            retired.Limiter.Dispose();
        }
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

        while (_draining.TryDequeue(out RegistryEntry? retired))
        {
            retired.Limiter.Dispose();
        }
    }

    private void EvictLeastRecentlyUsed()
    {
        // Linear scan — acceptable at _maxLimiters = 10_000 (microseconds).
        // Upgrading to a real LRU data structure is only warranted if the cap
        // grows by an order of magnitude. We pick the oldest entry that has no
        // permits in use or waiters queued — disposing a limiter with active
        // work would orphan the in-flight leases (CWE-672).
        string? lruKey = null;
        DateTimeOffset lruTimestamp = DateTimeOffset.MaxValue;

        foreach (KeyValuePair<string, RegistryEntry> kvp in _limiters)
        {
            if (kvp.Value.LastUsed >= lruTimestamp)
            {
                continue;
            }

            if (kvp.Value.HasOutstandingWork())
            {
                continue;
            }

            lruTimestamp = kvp.Value.LastUsed;
            lruKey = kvp.Key;
        }

        if (lruKey is not null && _limiters.TryRemove(lruKey, out RegistryEntry? evicted))
        {
            // Same two-phase retirement as EvictIdle: defer disposal so a fast-path
            // caller holding the reference cannot observe a disposed limiter.
            _draining.Enqueue(evicted);
            metrics.RecordEvicted("lru", 1);
        }
        // If every entry has outstanding work we deliberately do not evict and let the
        // new limiter push registry size to _maxLimiters + 1 transiently. The next idle
        // sweep will reclaim space; growing unboundedly is prevented by the lock-bounded
        // slow path.
    }

    private sealed class RegistryEntry(ConcurrencyLimiter limiter, int permitLimit, TimeProvider timeProvider)
    {
        public ConcurrencyLimiter Limiter { get; } = limiter;
        public DateTimeOffset LastUsed { get; private set; } = timeProvider.GetUtcNow();

        public void MarkUsed(TimeProvider tp) => LastUsed = tp.GetUtcNow();

        public bool HasOutstandingWork()
        {
            RateLimiterStatistics? stats = Limiter.GetStatistics();
            if (stats is null)
            {
                // Statistics unavailable — be conservative and treat the limiter as busy.
                return true;
            }

            return stats.CurrentQueuedCount > 0
                || stats.CurrentAvailablePermits < permitLimit;
        }
    }
}
