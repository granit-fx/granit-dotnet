using System.Text.Json;

namespace Granit.Caching.Options;

/// <summary>
/// Cache configuration options. Section <c>"Cache"</c> in <c>appsettings.json</c>.
/// </summary>
public sealed class CachingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cache";

    /// <summary>
    /// Application namespace prefix applied to all cache keys.
    /// Final formats: <c>{KeyPrefix}:t:{tenantId|host}:{userKey}</c> for <c>IFusionCache</c>
    /// entries (tenant segment added by the tenant-aware decorator) and
    /// <c>{KeyPrefix}:cond:t:{tenantId|host}:{userKey}</c> for <c>IConditionalCache</c> entries.
    /// Default: <c>"dd"</c>.
    /// </summary>
    public string KeyPrefix { get; set; } = "dd";

    /// <summary>
    /// Default absolute expiration relative to the time of caching, applied as the
    /// FusionCache default entry <c>Duration</c> when no per-entry options are passed.
    /// FusionCache has no sliding expiration — absolute duration is the only default.
    /// Default: 1 hour.
    /// </summary>
    public TimeSpan? DefaultAbsoluteExpirationRelativeToNow { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Enables AES-256 encryption for all types without a <see cref="CacheEncryptedAttribute"/>.
    /// Types marked <c>[CacheEncrypted]</c> are always encrypted (takes precedence).
    /// Types marked <c>[CacheEncrypted(false)]</c> are never encrypted (takes precedence).
    /// Default: <c>false</c> (disabled in dev/Memory).
    /// </summary>
    public bool EncryptValues { get; set; }

    /// <summary>
    /// Custom JSON serialization options. When <c>null</c>, the defaults are used.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; set; }
}
