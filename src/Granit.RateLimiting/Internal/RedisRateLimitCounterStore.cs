using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Granit.RateLimiting.Internal;

/// <summary>
/// Redis-backed rate limit counter store using Lua scripts for atomic operations.
/// Uses <c>redis.call('TIME')</c> for server-side timestamps to avoid clock skew.
/// Keys use Redis hash tags <c>{tenantId}</c> for Cluster compatibility.
/// </summary>
internal sealed partial class RedisRateLimitCounterStore(
    IConnectionMultiplexer redis,
    IOptions<GranitRateLimitingOptions> options,
    ILogger<RedisRateLimitCounterStore> logger) : IRateLimitCounterStore
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly GranitRateLimitingOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task<RateLimitResult> CheckAndIncrementAsync(
        string key,
        int permitLimit,
        TimeSpan window,
        RateLimitAlgorithm algorithm,
        RateLimitPolicyOptions policyOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            return algorithm switch
            {
                RateLimitAlgorithm.SlidingWindow => await ExecuteSlidingWindowAsync(key, permitLimit, window, cancellationToken).ConfigureAwait(false),
                RateLimitAlgorithm.FixedWindow => await ExecuteFixedWindowAsync(key, permitLimit, window, cancellationToken).ConfigureAwait(false),
                RateLimitAlgorithm.TokenBucket => await ExecuteTokenBucketAsync(key, policyOptions, cancellationToken).ConfigureAwait(false),
                _ => throw new NotSupportedException($"Rate limiting algorithm '{algorithm}' is not supported."),
            };
        }
        catch (RedisConnectionException ex)
        {
            return HandleRedisFailure(key, permitLimit, ex);
        }
        catch (RedisTimeoutException ex)
        {
            return HandleRedisFailure(key, permitLimit, ex);
        }
    }

    // =========================================================================
    // Sliding Window (sorted set)
    // =========================================================================

    private async Task<RateLimitResult> ExecuteSlidingWindowAsync(string key, int permitLimit, TimeSpan window, CancellationToken cancellationToken)
    {
        RedisResult result = await _db.ScriptEvaluateAsync(
            LuaScripts.SlidingWindow,
            [key],
            [(long)window.TotalMilliseconds, permitLimit])
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        var values = (RedisResult[])result!;
        int count = (int)values[0];
        bool allowed = count <= permitLimit;
        int remaining = Math.Max(0, permitLimit - count);

        if (!allowed)
        {
            long oldestMs = (long)values[1];
            var retryAfter = TimeSpan.FromMilliseconds(Math.Max(oldestMs, 1));
            return new RateLimitResult(false, 0, permitLimit, retryAfter);
        }

        return new RateLimitResult(true, remaining, permitLimit, TimeSpan.Zero);
    }

    // =========================================================================
    // Fixed Window (INCR + EXPIRE)
    // =========================================================================

    private async Task<RateLimitResult> ExecuteFixedWindowAsync(string key, int permitLimit, TimeSpan window, CancellationToken cancellationToken)
    {
        RedisResult result = await _db.ScriptEvaluateAsync(
            LuaScripts.FixedWindow,
            [key],
            [(long)window.TotalMilliseconds, permitLimit])
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        var values = (RedisResult[])result!;
        int count = (int)values[0];
        long ttlMs = (long)values[1];
        bool allowed = count <= permitLimit;
        int remaining = Math.Max(0, permitLimit - count);

        return allowed
            ? new RateLimitResult(true, remaining, permitLimit, TimeSpan.Zero)
            : new RateLimitResult(false, 0, permitLimit, TimeSpan.FromMilliseconds(Math.Max(ttlMs, 1)));
    }

    // =========================================================================
    // Token Bucket (hash + Lua)
    // =========================================================================

    private async Task<RateLimitResult> ExecuteTokenBucketAsync(string key, RateLimitPolicyOptions policyOptions, CancellationToken cancellationToken)
    {
        RedisResult result = await _db.ScriptEvaluateAsync(
            LuaScripts.TokenBucket,
            [key],
            [policyOptions.TokenLimit, policyOptions.TokensPerPeriod, (long)policyOptions.ReplenishmentPeriod.TotalMilliseconds])
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        var values = (RedisResult[])result!;
        int allowed = (int)values[0];
        int tokens = (int)values[1];
        long retryMs = (long)values[2];

        return allowed == 1
            ? new RateLimitResult(true, tokens, policyOptions.TokenLimit, TimeSpan.Zero)
            : new RateLimitResult(false, 0, policyOptions.TokenLimit, TimeSpan.FromMilliseconds(Math.Max(retryMs, 1)));
    }

    // =========================================================================
    // Redis failure handling
    // =========================================================================

    private RateLimitResult HandleRedisFailure(string key, int permitLimit, Exception ex)
    {
        LogRedisFailure(key, ex);

        return _options.FallbackOnCounterStoreFailure switch
        {
            CounterStoreFailureBehavior.Allow => new RateLimitResult(true, permitLimit, permitLimit, TimeSpan.Zero),
            _ => new RateLimitResult(false, 0, permitLimit, TimeSpan.FromSeconds(1)),
        };
    }

    // =========================================================================
    // Source-generated logger messages
    // =========================================================================

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Redis unavailable for rate limiting key {Key}. Applying fallback behavior.")]
    private partial void LogRedisFailure(string key, Exception exception);
}
