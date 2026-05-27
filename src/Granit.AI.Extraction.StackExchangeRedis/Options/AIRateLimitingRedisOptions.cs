using System.ComponentModel.DataAnnotations;

namespace Granit.AI.Extraction.StackExchangeRedis.Options;

/// <summary>
/// Configuration for the Redis-backed <c>IAICallRateLimiter</c>. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class AIRateLimitingRedisOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AI:RateLimiting:Redis";

    /// <summary>
    /// When <c>false</c>, the module stays inert and the in-memory limiter from
    /// <c>Granit.AI.Extraction</c> remains in effect. Default: <c>true</c> — referencing
    /// the package opts a host into distributed rate limiting.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Key prefix for every bucket, isolating this application's buckets from any other
    /// Granit app sharing the same Redis instance. A collision would let one app's calls
    /// count against another's ceiling. Default: <c>"granit:ai:ratelimit:"</c>.
    /// </summary>
    /// <remarks>
    /// Override per app (e.g. <c>"granit:ai:ratelimit:billing:"</c>) when multiple Granit
    /// services share one Redis. The bucket key the limiter receives is appended after
    /// this prefix.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string KeyPrefix { get; set; } = "granit:ai:ratelimit:";

    /// <summary>
    /// When Redis is unreachable (connection or timeout error), <c>false</c> (the default)
    /// denies the call — the consumer gracefully falls through to its non-AI path, which
    /// protects the cost ceiling during an outage. Set <c>true</c> to allow calls through
    /// instead (favours availability over the wallet). Default: <c>false</c> (fail closed).
    /// </summary>
    public bool AllowOnRedisFailure { get; set; }
}
