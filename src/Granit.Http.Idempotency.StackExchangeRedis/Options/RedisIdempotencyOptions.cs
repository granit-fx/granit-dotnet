namespace Granit.Http.Idempotency.StackExchangeRedis.Options;

/// <summary>
/// Configuration options for the Redis idempotency store.
/// Section <c>"Http:Idempotency:Redis"</c> in <c>appsettings.json</c>.
/// </summary>
public sealed class RedisIdempotencyOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Http:Idempotency:Redis";

    /// <summary>
    /// Enables or disables the Redis idempotency store.
    /// When <c>false</c>, the in-memory store remains active (Development-only —
    /// the startup guard fails loud elsewhere).
    /// Default: <c>true</c>.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Redis connection string in StackExchange.Redis format.
    /// Examples: <c>"localhost:6379"</c>, <c>"redis-service:6379,password=secret"</c>.
    /// In production: provided via Vault or environment variables.
    /// Ignored when the host already registers an <c>IConnectionMultiplexer</c>
    /// (e.g. via <c>Granit.Caching.StackExchangeRedis</c>) — the existing connection is reused.
    /// </summary>
    public string Configuration { get; set; } = "localhost:6379";

    /// <summary>
    /// Redis key prefix for idempotency entries, prepended to the middleware-composed key
    /// (<c>{prefix}:{tenant}:{user}:{method}:{route}:{keyHash}</c>). Allows multiple
    /// applications to share the same Redis instance. Tenant isolation already comes from
    /// the middleware key — this prefix must stay application-scoped, never tenant-scoped.
    /// Default: <c>"dd:"</c>.
    /// </summary>
    public string InstanceName { get; set; } = "dd:";

    /// <summary>
    /// Enforce TLS for the Redis connection.
    /// Default: <c>true</c> (production-safe).
    /// Set to <c>false</c> for local development without TLS.
    /// </summary>
    public bool RequireTls { get; set; } = true;
}
