namespace Granit.AI.Extraction.StackExchangeRedis.Internal;

/// <summary>
/// Redis Lua scripts for the AI call rate limiter. Uses <c>redis.call('TIME')</c> for
/// server-side timestamps so the sliding window is immune to client clock skew across
/// replicas.
/// </summary>
internal static class LuaScripts
{
    /// <summary>
    /// Atomic sliding-window admit-or-deny over a sorted set.
    /// <list type="bullet">
    ///   <item><c>KEYS[1]</c> = bucket key</item>
    ///   <item><c>ARGV[1]</c> = window duration in milliseconds</item>
    ///   <item><c>ARGV[2]</c> = call limit within the window</item>
    /// </list>
    /// Returns <c>1</c> when the call is admitted (a member was added), <c>0</c> when the
    /// bucket already holds <c>limit</c> entries within the trailing window.
    /// </summary>
    public const string SlidingWindowAdmit = """
        local key = KEYS[1]
        local window_ms = tonumber(ARGV[1])
        local limit = tonumber(ARGV[2])
        local time = redis.call('TIME')
        local now = tonumber(time[1]) * 1000 + math.floor(tonumber(time[2]) / 1000)
        local window_start = now - window_ms

        redis.call('ZREMRANGEBYSCORE', key, '-inf', window_start)

        local count = redis.call('ZCARD', key)
        if count >= limit then
            redis.call('PEXPIRE', key, window_ms)
            return 0
        end

        redis.call('ZADD', key, now, now .. '-' .. math.random(1000000))
        redis.call('PEXPIRE', key, window_ms)
        return 1
        """;
}
