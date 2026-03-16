namespace Granit.Caching.StackExchangeRedis.Options;

/// <summary>
/// Configuration options for the Redis provider. Section <c>"Cache:Redis"</c> in <c>appsettings.json</c>.
/// </summary>
public sealed class RedisCachingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cache:Redis";

    /// <summary>
    /// Enables or disables the Redis provider.
    /// When <c>false</c>, the Memory provider remains active.
    /// Useful for disabling Redis in development without changing the loaded module.
    /// Default: <c>true</c>.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Redis connection string in StackExchange.Redis format.
    /// Examples: <c>"localhost:6379"</c>, <c>"redis-service:6379,password=secret"</c>.
    /// In production: provided via Vault or environment variables.
    /// </summary>
    public string Configuration { get; set; } = "localhost:6379";

    /// <summary>
    /// Redis instance name prefix. Prepended to all keys stored in Redis.
    /// Allows multiple applications to share the same Redis instance without key collisions.
    /// Default: <c>"dd:"</c>.
    /// </summary>
    public string InstanceName { get; set; } = "dd:";
}
