namespace Granit.RateLimiting.Internal;

/// <summary>
/// Redis Lua scripts for atomic rate limiting operations.
/// All scripts use <c>redis.call('TIME')</c> for server-side timestamps to avoid clock skew.
/// </summary>
internal static class LuaScripts
{
    /// <summary>
    /// Sliding window using a sorted set.
    /// KEYS[1] = rate limit key
    /// ARGV[1] = window duration in milliseconds
    /// ARGV[2] = permit limit
    /// Returns: {count_after_add, time_until_oldest_expires_ms}
    /// </summary>
    public const string SlidingWindow = """
        local key = KEYS[1]
        local window_ms = tonumber(ARGV[1])
        local limit = tonumber(ARGV[2])
        local time = redis.call('TIME')
        local now = tonumber(time[1]) * 1000 + math.floor(tonumber(time[2]) / 1000)
        local window_start = now - window_ms

        redis.call('ZREMRANGEBYSCORE', key, '-inf', window_start)

        local count = redis.call('ZCARD', key)

        if count < limit then
            redis.call('ZADD', key, now, now .. '-' .. math.random(1000000))
            count = count + 1
        end

        redis.call('PEXPIRE', key, window_ms)

        local oldest = 0
        if count > limit then
            local members = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
            if #members >= 2 then
                oldest = window_ms - (now - tonumber(members[2]))
            end
        end

        return {count, oldest}
        """;

    /// <summary>
    /// Fixed window using INCR + PEXPIRE.
    /// KEYS[1] = rate limit key (includes window alignment)
    /// ARGV[1] = window duration in milliseconds
    /// ARGV[2] = permit limit
    /// Returns: {count, ttl_remaining_ms}
    /// </summary>
    public const string FixedWindow = """
        local key = KEYS[1]
        local window_ms = tonumber(ARGV[1])
        local limit = tonumber(ARGV[2])

        local count = redis.call('INCR', key)

        if count == 1 then
            redis.call('PEXPIRE', key, window_ms)
        end

        local ttl = redis.call('PTTL', key)
        if ttl < 0 then
            redis.call('PEXPIRE', key, window_ms)
            ttl = window_ms
        end

        return {count, ttl}
        """;

    /// <summary>
    /// Token bucket using a hash with tokens and last_refill.
    /// KEYS[1] = rate limit key
    /// ARGV[1] = token limit (max bucket size)
    /// ARGV[2] = tokens per period
    /// ARGV[3] = replenishment period in milliseconds
    /// Returns: {allowed (0/1), tokens_remaining, retry_after_ms}
    /// </summary>
#pragma warning disable GRSEC003 // "TokenBucket" is a rate-limiting algorithm name, not a credential.
    public const string TokenBucket = """
        local key = KEYS[1]
        local token_limit = tonumber(ARGV[1])
        local tokens_per_period = tonumber(ARGV[2])
        local period_ms = tonumber(ARGV[3])
        local time = redis.call('TIME')
        local now = tonumber(time[1]) * 1000 + math.floor(tonumber(time[2]) / 1000)

        local data = redis.call('HMGET', key, 'tokens', 'last_refill')
        local tokens = tonumber(data[1])
        local last_refill = tonumber(data[2])

        if tokens == nil then
            tokens = token_limit
            last_refill = now
        end

        local elapsed = now - last_refill
        if elapsed > 0 and period_ms > 0 then
            local periods = math.floor(elapsed / period_ms)
            if periods > 0 then
                tokens = math.min(token_limit, tokens + periods * tokens_per_period)
                last_refill = last_refill + periods * period_ms
            end
        end

        local allowed = 0
        local retry_ms = 0

        if tokens > 0 then
            tokens = tokens - 1
            allowed = 1
        else
            retry_ms = period_ms - (now - last_refill)
            if retry_ms < 1 then retry_ms = 1 end
        end

        redis.call('HSET', key, 'tokens', tokens, 'last_refill', last_refill)
        redis.call('PEXPIRE', key, period_ms * math.ceil(token_limit / tokens_per_period) + period_ms)

        return {allowed, tokens, retry_ms}
        """;
#pragma warning restore GRSEC003
}
