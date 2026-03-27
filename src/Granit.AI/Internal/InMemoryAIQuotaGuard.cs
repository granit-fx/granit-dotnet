using System.Collections.Concurrent;
using System.Threading;
using Granit.AI.Options;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.AI.Internal;

/// <summary>
/// In-memory per-tenant AI quota guard using a sliding window counter.
/// </summary>
/// <remarks>
/// Uses <see cref="ConcurrentDictionary{TKey,TValue}"/> with timestamp-based cleanup.
/// Suitable for single-instance deployments. For multi-instance, replace with a
/// distributed implementation backed by Redis or a distributed cache abstraction.
/// </remarks>
internal sealed class InMemoryAIQuotaGuard(
    IOptions<AIQuotaOptions> options,
    ICurrentTenant currentTenant,
    IClock clock) : IAIQuotaGuard
{
    private readonly ConcurrentDictionary<string, TenantWindow> _windows = new();

    public Task<AIQuotaResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        int maxRequests = options.Value.MaxRequestsPerTenantPerHour;

        if (maxRequests <= 0)
        {
            return Task.FromResult(AIQuotaResult.Allowed);
        }

        string tenantKey = currentTenant.IsAvailable && currentTenant.Id is not null
            ? currentTenant.Id.Value.ToString()
            : "global";

        DateTimeOffset now = clock.Now;
        TenantWindow window = _windows.GetOrAdd(tenantKey, _ => new TenantWindow());

        int count = window.IncrementAndCount(now, maxRequests);

        if (count > maxRequests)
        {
            return Task.FromResult(AIQuotaResult.Denied(
                $"AI quota exceeded: {maxRequests} requests/hour for tenant '{tenantKey}'. " +
                "Try again later."));
        }

        return Task.FromResult(new AIQuotaResult(true, Remaining: maxRequests - count));
    }

    /// <summary>
    /// Sliding window counter per tenant. Thread-safe via <see cref="Lock"/>.
    /// </summary>
    private sealed class TenantWindow
    {
        private readonly Lock _lock = new();
        private readonly Queue<DateTimeOffset> _timestamps = new();

        /// <summary>
        /// Records the current request and returns the count within the rolling hour window.
        /// </summary>
        public int IncrementAndCount(DateTimeOffset now, int capacity)
        {
            DateTimeOffset windowStart = now.AddHours(-1);

            lock (_lock)
            {
                // Evict expired entries.
                while (_timestamps.Count > 0 && _timestamps.Peek() < windowStart)
                {
                    _timestamps.Dequeue();
                }

                _timestamps.Enqueue(now);
                return _timestamps.Count;
            }
        }
    }
}
