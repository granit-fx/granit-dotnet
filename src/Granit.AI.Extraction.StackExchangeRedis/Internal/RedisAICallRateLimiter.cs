using Granit.AI.Extraction.RateLimiting;
using Granit.AI.Extraction.StackExchangeRedis.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Granit.AI.Extraction.StackExchangeRedis.Internal;

/// <summary>
/// Distributed <see cref="IAICallRateLimiter"/> backed by a Redis sorted-set sliding
/// window. Unlike the in-memory default, the per-tenant hourly ceiling is enforced once
/// across every replica, closing the denial-of-wallet gap where the effective limit was
/// <c>N × MaxAICallsPerHourPerTenant</c> for <c>N</c> pods.
/// </summary>
/// <remarks>
/// Shares the host's <see cref="IConnectionMultiplexer"/> (the same connection used by
/// <c>Granit.Caching.StackExchangeRedis</c> when present). Bucket keys are wrapped in a
/// Redis Cluster hash tag so all operations for one bucket land on the same slot.
/// </remarks>
internal sealed partial class RedisAICallRateLimiter(
    IConnectionMultiplexer redis,
    IOptions<AIRateLimitingRedisOptions> options,
    ILogger<RedisAICallRateLimiter> logger) : IAICallRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly IDatabase _db = redis.GetDatabase();
    private readonly AIRateLimitingRedisOptions _options = options.Value;

    public async ValueTask<bool> TryAcquireAsync(
        string bucketKey,
        int maxCallsPerHour,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCallsPerHour);
        cancellationToken.ThrowIfCancellationRequested();

        var key = (RedisKey)BuildKey(bucketKey);

        try
        {
            RedisResult result = await _db.ScriptEvaluateAsync(
                LuaScripts.SlidingWindowAdmit,
                [key],
                [(long)Window.TotalMilliseconds, maxCallsPerHour])
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            return (long)result == 1;
        }
        catch (RedisConnectionException ex)
        {
            return HandleRedisFailure(bucketKey, ex);
        }
        catch (RedisTimeoutException ex)
        {
            return HandleRedisFailure(bucketKey, ex);
        }
    }

    // Hash-tag the whole composite key so every command for one bucket maps to a single
    // Cluster slot. The prefix isolates this app's buckets from other Granit apps sharing
    // the Redis instance.
    private string BuildKey(string bucketKey) => $"{_options.KeyPrefix}{{{bucketKey}}}";

    private bool HandleRedisFailure(string bucketKey, Exception ex)
    {
        LogRedisFailure(bucketKey, _options.AllowOnRedisFailure ? "allow" : "deny", ex.GetType().Name);
        return _options.AllowOnRedisFailure;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Redis unavailable for AI rate-limit bucket {BucketKey}; applying {FailureMode} fallback (exception type: {ExceptionType}).")]
    private partial void LogRedisFailure(string bucketKey, string failureMode, string exceptionType);
}
