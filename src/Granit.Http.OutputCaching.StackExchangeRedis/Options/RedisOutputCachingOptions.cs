namespace Granit.Http.OutputCaching.StackExchangeRedis.Options;

/// <summary>
/// Configuration options for the Redis output cache store.
/// Section <c>"OutputCaching:Redis"</c> in <c>appsettings.json</c>.
/// </summary>
public sealed class RedisOutputCachingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OutputCaching:Redis";

    /// <summary>
    /// Enables or disables the Redis output cache store.
    /// When <c>false</c>, the in-memory store remains active.
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
    /// Redis key prefix for output cache entries.
    /// Allows multiple applications to share the same Redis instance.
    /// Default: <c>"dd:oc:"</c>.
    /// </summary>
    public string InstanceName { get; set; } = "dd:oc:";

    /// <summary>
    /// Enforce TLS for the Redis connection.
    /// Default: <c>true</c> (production-safe).
    /// Set to <c>false</c> for local development without TLS.
    /// </summary>
    public bool RequireTls { get; set; } = true;
}
