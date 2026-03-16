using Granit.Caching;
using Granit.Caching.Hybrid.Options;
using Granit.Caching.Internal;
using Granit.Caching.Options;
using Granit.Timing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Caching.Hybrid;

/// <summary>
/// Implementation of <see cref="ICacheService{TCacheItem}"/> backed by <see cref="HybridCache"/> (.NET 9).
/// Provides a two-level cache: L1 (per-pod local memory) + L2 (<c>IDistributedCache</c>, Redis).
/// </summary>
/// <remarks>
/// <para>
/// L1+L2 behaviour on multi-pod Kubernetes clusters:
/// <list type="bullet">
///   <item>L1 read &lt; 1 ms (pod-local memory)</item>
///   <item>L2 read ~2 ms (Redis shared across pods)</item>
///   <item>Full cache miss: factory is invoked and the result is written to both L1 and L2</item>
/// </list>
/// </para>
/// <para>
/// Invalidation: <see cref="RemoveAsync(string, CancellationToken)"/> clears L2 (Redis) and the L1
/// cache of the calling pod. L1 caches on other pods expire after at most
/// <see cref="HybridCachingOptions.LocalCacheExpiration"/> (30 s by default).
/// </para>
/// <para>
/// Stampede protection: built into <c>HybridCache</c>; no <c>SemaphoreSlim</c> is needed.
/// </para>
/// <para>
/// ISO 27001 encryption: not supported by this provider. <c>HybridCache</c> manages L2 serialization
/// internally — there is no hook to intercept <c>byte[]</c> encryption.
/// For sensitive data requiring encryption at rest, use
/// <c>GranitCachingRedisModule</c> (pure Redis provider with <see cref="ICacheValueEncryptor"/>).
/// </para>
/// </remarks>
/// <typeparam name="TCacheItem">The type of the cached item. Must be a class.</typeparam>
/// <remarks>
/// Initializes a new instance of <see cref="HybridCacheService{TCacheItem}"/>.
/// </remarks>
/// <param name="hybridCache">The L1+L2 hybrid cache provided by the .NET 9 runtime.</param>
/// <param name="options">Global cache options.</param>
/// <param name="logger">Structured logger.</param>
/// <param name="clock">UTC clock used to compute absolute expirations.</param>
public partial class HybridCacheService<TCacheItem>(
    HybridCache hybridCache,
    IOptions<CachingOptions> options,
    ILogger<HybridCacheService<TCacheItem>> logger,
    IClock clock) : ICacheService<TCacheItem>
    where TCacheItem : class
{
    private readonly HybridCache _hybridCache = hybridCache;
    private readonly IOptions<CachingOptions> _options = options;
    private readonly ILogger<HybridCacheService<TCacheItem>> _logger = logger;
    private readonly IClock _clock = clock;
    private readonly string _cacheName = CacheNameProvider.GetCacheName(typeof(TCacheItem));

    /// <inheritdoc/>
    public async Task<TCacheItem?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        string compositeKey = BuildKey(key);
        LogGet(_logger, compositeKey);

        // HybridCache.GetOrCreateAsync ne permet pas de faire un "Get seul" natif.
        // On passe une factory qui retourne null pour simuler un GetOrDefault.
        // Le résultat null est mis en cache brièvement (L1 TTL court) — comportement acceptable.
        TCacheItem? result = await _hybridCache.GetOrCreateAsync<TCacheItem?>(
            compositeKey,
            _ => ValueTask.FromResult<TCacheItem?>(null),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc/>
    public async Task<TCacheItem> GetOrAddAsync(
        string key,
        Func<CancellationToken, Task<TCacheItem>> factory,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string compositeKey = BuildKey(key);
        LogGetOrAdd(_logger, compositeKey);

        HybridCacheEntryOptions? hybridOptions = options is not null
            ? BuildHybridOptions(options)
            : null;

        TCacheItem result = await _hybridCache.GetOrCreateAsync(
            compositeKey,
            async (innerCt) =>
            {
                LogFactory(_logger, compositeKey);
                TCacheItem value = await factory(innerCt).ConfigureAwait(false);
                return value;
            },
            hybridOptions,
            cancellationToken: cancellationToken);

        return result;
    }

    /// <inheritdoc/>
    public async Task SetAsync(
        string key,
        TCacheItem value,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string compositeKey = BuildKey(key);
        LogSet(_logger, compositeKey);

        HybridCacheEntryOptions? hybridOptions = options is not null
            ? BuildHybridOptions(options)
            : null;

        await _hybridCache.SetAsync(compositeKey, value, hybridOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        string compositeKey = BuildKey(key);
        LogRemove(_logger, compositeKey);

        await _hybridCache.RemoveAsync(compositeKey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task RefreshAsync(string key, CancellationToken cancellationToken = default)
    {
        // HybridCache ne supporte pas nativement le refresh (sliding expiration sur IDistributedCache).
        // Un SetAsync avec la valeur actuelle est l'équivalent fonctionnel.
        string compositeKey = BuildKey(key);
        LogRefreshNotSupported(_logger, compositeKey);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "HybridCache GET {Key}")]
    private static partial void LogGet(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "HybridCache GET_OR_ADD {Key}")]
    private static partial void LogGetOrAdd(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "HybridCache FACTORY {Key}")]
    private static partial void LogFactory(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "HybridCache SET {Key}")]
    private static partial void LogSet(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "HybridCache REMOVE {Key}")]
    private static partial void LogRemove(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HybridCache REFRESH not natively supported for {Key}. Use GetOrAddAsync instead.")]
    private static partial void LogRefreshNotSupported(ILogger logger, string key);

    private string BuildKey(string userKey) =>
        $"{_options.Value.KeyPrefix}:{_cacheName}:{userKey}";

    private HybridCacheEntryOptions BuildHybridOptions(DistributedCacheEntryOptions distributed) =>
        new()
        {
            Expiration = distributed.AbsoluteExpirationRelativeToNow
                ?? (distributed.AbsoluteExpiration.HasValue
                    ? distributed.AbsoluteExpiration.Value - _clock.Now
                    : null),
            LocalCacheExpiration = distributed.SlidingExpiration,
        };
}
