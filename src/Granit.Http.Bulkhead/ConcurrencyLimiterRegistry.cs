using System.Collections.Concurrent;
using System.Threading.RateLimiting;

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
/// </remarks>
public sealed class ConcurrencyLimiterRegistry(TimeProvider timeProvider) : IDisposable
{
    private readonly ConcurrentDictionary<string, RegistryEntry> _limiters = new(StringComparer.Ordinal);

    /// <summary>
    /// Acquires a concurrency permit for the given key. Creates the limiter on first access.
    /// </summary>
    public ValueTask<RateLimitLease> AcquireAsync(
        string key,
        int permitLimit,
        int queueLimit,
        CancellationToken cancellationToken)
    {
        RegistryEntry entry = _limiters.GetOrAdd(key, static (_, args) => new RegistryEntry(
            new ConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = args.permitLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = args.queueLimit,
            }), args.timeProvider), (permitLimit, queueLimit, timeProvider));

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

    private sealed class RegistryEntry(ConcurrencyLimiter limiter, TimeProvider timeProvider)
    {
        public ConcurrencyLimiter Limiter { get; } = limiter;
        public DateTimeOffset LastUsed { get; private set; } = timeProvider.GetUtcNow();

        public void MarkUsed(TimeProvider tp) => LastUsed = tp.GetUtcNow();
    }
}
