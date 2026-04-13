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
    /// Name of the connection string in <c>ConnectionStrings:{name}</c>.
    /// When set and the named connection string exists, it takes precedence
    /// over <see cref="Configuration"/>. This supports .NET Aspire resource
    /// injection out-of-the-box (<c>builder.AddRedis("cache")</c>).
    /// Set to <c>null</c> to disable connection string resolution and use
    /// <see cref="Configuration"/> directly.
    /// Default: <c>"cache"</c>.
    /// </summary>
#pragma warning disable GRSEC003 // Config key name, not a secret value
    public string? ConnectionStringName { get; set; } = "cache";
#pragma warning restore GRSEC003

    /// <summary>
    /// Explicit Redis connection string in StackExchange.Redis format.
    /// Used when <see cref="ConnectionStringName"/> is <c>null</c> or the named
    /// connection string is not found in configuration.
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
