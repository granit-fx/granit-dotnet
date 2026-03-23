using System.Collections.Concurrent;
using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Options;

namespace Granit.RateLimiting.Internal;

/// <summary>
/// In-memory rate limit counter store for development and testing.
/// Not suitable for production multi-instance deployments — counters are not shared.
/// </summary>
internal sealed class InMemoryRateLimitCounterStore(TimeProvider timeProvider) : IRateLimitCounterStore
{
    private readonly ConcurrentDictionary<string, SlidingWindowState> _slidingWindows = new();
    private readonly ConcurrentDictionary<string, FixedWindowState> _fixedWindows = new();
    private readonly ConcurrentDictionary<string, TokenBucketState> _tokenBuckets = new();
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public Task<RateLimitResult> CheckAndIncrementAsync(
        string key,
        int permitLimit,
        TimeSpan window,
        RateLimitAlgorithm algorithm,
        RateLimitPolicyOptions policyOptions,
        CancellationToken cancellationToken)
    {
        RateLimitResult result = algorithm switch
        {
            RateLimitAlgorithm.SlidingWindow => CheckSlidingWindow(key, permitLimit, window),
            RateLimitAlgorithm.FixedWindow => CheckFixedWindow(key, permitLimit, window),
            RateLimitAlgorithm.TokenBucket => CheckTokenBucket(key, policyOptions),
            _ => throw new NotSupportedException($"Rate limiting algorithm '{algorithm}' is not supported."),
        };

        return Task.FromResult(result);
    }

    // =========================================================================
    // Sliding Window
    // =========================================================================

    private RateLimitResult CheckSlidingWindow(string key, int permitLimit, TimeSpan window)
    {
        SlidingWindowState state = _slidingWindows.GetOrAdd(key, _ => new SlidingWindowState());
        long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        long windowStart = now - (long)window.TotalMilliseconds;

        lock (state.SyncLock)
        {
            state.Timestamps.RemoveAll(t => t < windowStart);
            int count = state.Timestamps.Count;

            if (count >= permitLimit)
            {
                long oldest = state.Timestamps.Count > 0 ? state.Timestamps[0] : now;
                var retryAfter = TimeSpan.FromMilliseconds(oldest - windowStart);
                return new RateLimitResult(false, 0, permitLimit, retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(1));
            }

            state.Timestamps.Add(now);
            return new RateLimitResult(true, permitLimit - count - 1, permitLimit, TimeSpan.Zero);
        }
    }

    // =========================================================================
    // Fixed Window
    // =========================================================================

    private RateLimitResult CheckFixedWindow(string key, int permitLimit, TimeSpan window)
    {
        FixedWindowState state = _fixedWindows.GetOrAdd(key, _ => new FixedWindowState());
        long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        lock (state.SyncLock)
        {
            long windowMs = (long)window.TotalMilliseconds;
            long windowStart = now / windowMs * windowMs;

            if (state.WindowStart != windowStart)
            {
                state.WindowStart = windowStart;
                state.Count = 0;
            }

            if (state.Count >= permitLimit)
            {
                var retryAfter = TimeSpan.FromMilliseconds(windowStart + windowMs - now);
                return new RateLimitResult(false, 0, permitLimit, retryAfter);
            }

            state.Count++;
            return new RateLimitResult(true, permitLimit - state.Count, permitLimit, TimeSpan.Zero);
        }
    }

    // =========================================================================
    // Token Bucket
    // =========================================================================

    private RateLimitResult CheckTokenBucket(string key, RateLimitPolicyOptions policyOptions)
    {
        TokenBucketState state = _tokenBuckets.GetOrAdd(key, _ => new TokenBucketState
        {
            Tokens = policyOptions.TokenLimit,
            LastRefill = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
        });

        long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        lock (state.SyncLock)
        {
            long elapsed = now - state.LastRefill;
            long periodMs = (long)policyOptions.ReplenishmentPeriod.TotalMilliseconds;

            if (periodMs > 0 && elapsed > 0)
            {
                long periods = elapsed / periodMs;
                if (periods > 0)
                {
                    state.Tokens = Math.Min(policyOptions.TokenLimit, state.Tokens + (int)(periods * policyOptions.TokensPerPeriod));
                    state.LastRefill += periods * periodMs;
                }
            }

            if (state.Tokens <= 0)
            {
                long nextRefill = periodMs - (now - state.LastRefill);
                return new RateLimitResult(false, 0, policyOptions.TokenLimit, TimeSpan.FromMilliseconds(Math.Max(nextRefill, 1)));
            }

            state.Tokens--;
            return new RateLimitResult(true, state.Tokens, policyOptions.TokenLimit, TimeSpan.Zero);
        }
    }

    // =========================================================================
    // State classes
    // =========================================================================

    private sealed class SlidingWindowState
    {
        public Lock SyncLock { get; } = new();
        public List<long> Timestamps { get; } = [];
    }

    private sealed class FixedWindowState
    {
        public Lock SyncLock { get; } = new();
        public long WindowStart { get; set; }
        public int Count { get; set; }
    }

    private sealed class TokenBucketState
    {
        public Lock SyncLock { get; } = new();
        public int Tokens { get; set; }
        public long LastRefill { get; set; }
    }
}
