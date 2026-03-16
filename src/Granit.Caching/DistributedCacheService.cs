using System.Reflection;
using System.Text.Json;
using Granit.Caching.Internal;
using Granit.Caching.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Caching;

/// <summary>
/// Implementation of <see cref="ICacheService{TCacheItem}"/> backed by <see cref="IDistributedCache"/>.
/// </summary>
/// <remarks>
/// Features:
/// <list type="bullet">
///   <item>Automatic JSON serialization/deserialization via <c>System.Text.Json</c></item>
///   <item>Composite key: <c>{KeyPrefix}:{CacheName}:{userKey}</c></item>
///   <item>Stampede protection: double-check locking + <see cref="SemaphoreSlim"/> in a dedicated <see cref="IMemoryCache"/> (TTL 30 s)</item>
///   <item>Opt-in AES-256-CBC encryption via <see cref="CacheEncryptedAttribute"/> or <see cref="CachingOptions.EncryptValues"/></item>
/// </list>
/// The injected <see cref="IMemoryCache"/> is dedicated to stampede locks (DI key: <c>Granit.Caching.Locks</c>)
/// and is separate from the application memory cache to avoid interference.
/// </remarks>
/// <param name="cache">The distributed cache provider (Memory or Redis).</param>
/// <param name="lockCache">Memory cache dedicated to stampede locks.</param>
/// <param name="encryptor">AES encryptor (no-op in dev, AES-256 in prod).</param>
/// <param name="options">Global cache options.</param>
/// <param name="logger">Logger for diagnostics.</param>
public partial class DistributedCacheService<TCacheItem>(
    IDistributedCache cache,
    [FromKeyedServices(DistributedCacheService<TCacheItem>.LockCacheKey)] IMemoryCache lockCache,
    ICacheValueEncryptor encryptor,
    IOptions<CachingOptions> options,
    ILogger<DistributedCacheService<TCacheItem>> logger) : ICacheService<TCacheItem>
    where TCacheItem : class
{
    internal const string LockCacheKey = "Granit.Caching.Locks";

    private readonly IDistributedCache _cache = cache;
    private readonly IMemoryCache _lockCache = lockCache;
    private readonly ICacheValueEncryptor _encryptor = encryptor;
    private readonly IOptions<CachingOptions> _options = options;
    private readonly ILogger<DistributedCacheService<TCacheItem>> _logger = logger;
    private readonly bool _shouldEncrypt = CacheEncryptionResolver.ShouldEncrypt(typeof(TCacheItem), options.Value);
    private readonly string _keyPrefix = $"{options.Value.KeyPrefix}:{CacheNameProvider.GetCacheName(typeof(TCacheItem))}:";

    /// <inheritdoc/>
    public async Task<TCacheItem?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        string compositeKey = BuildKey(key);
        byte[]? bytes = await _cache.GetAsync(compositeKey, cancellationToken).ConfigureAwait(false);

        if (bytes is null)
        {
            return null;
        }

        if (_shouldEncrypt)
        {
            bytes = _encryptor.Decrypt(bytes);
        }

        return JsonSerializer.Deserialize<TCacheItem>(bytes, _options.Value.JsonOptions);
    }

    /// <inheritdoc/>
    public async Task<TCacheItem> GetOrAddAsync(
        string key,
        Func<CancellationToken, Task<TCacheItem>> factory,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Vérification rapide sans verrou (chemin chaud — évite la contention)
        TCacheItem? cached = await GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        // 2. Acquisition du verrou stocké dans IMemoryCache (TTL 30 s — auto-nettoyage par le GC)
        string compositeKey = BuildKey(key);
        SemaphoreSlim semaphore = GetOrCreateLock(compositeKey);
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 3. Double-check locking : un autre thread peut avoir rempli le cache pendant l'attente
            cached = await GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }

            // 4. Exécution de la factory (garantie une seule fois sous concurrence)
            TCacheItem value = await factory(cancellationToken).ConfigureAwait(false);
            await SetAsync(key, value, options, cancellationToken).ConfigureAwait(false);

            LogCacheMiss(_logger, compositeKey);

            return value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync(
        string key,
        TCacheItem value,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string compositeKey = BuildKey(key);
        DistributedCacheEntryOptions entryOptions = options ?? BuildDefaultOptions();

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, _options.Value.JsonOptions);

        if (_shouldEncrypt)
        {
            bytes = _encryptor.Encrypt(bytes);
        }

        await _cache.SetAsync(compositeKey, bytes, entryOptions, cancellationToken).ConfigureAwait(false);

        LogCacheSet(_logger, compositeKey);
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        _cache.RemoveAsync(BuildKey(key), cancellationToken);

    /// <inheritdoc/>
    public Task RefreshAsync(string key, CancellationToken cancellationToken = default) =>
        _cache.RefreshAsync(BuildKey(key), cancellationToken);

    private string BuildKey(string userKey) =>
        string.Concat(_keyPrefix, userKey);

    private DistributedCacheEntryOptions BuildDefaultOptions()
    {
        CachingOptions opts = _options.Value;
        DistributedCacheEntryOptions entry = new();

        if (opts.DefaultAbsoluteExpirationRelativeToNow.HasValue)
        {
            entry.AbsoluteExpirationRelativeToNow = opts.DefaultAbsoluteExpirationRelativeToNow;
        }

        if (opts.DefaultSlidingExpiration.HasValue)
        {
            entry.SlidingExpiration = opts.DefaultSlidingExpiration;
        }

        return entry;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache miss resolved via factory: {Key}")]
    private static partial void LogCacheMiss(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache set: {Key}")]
    private static partial void LogCacheSet(ILogger logger, string key);

    private SemaphoreSlim GetOrCreateLock(string compositeKey) =>
        _lockCache.GetOrCreate(
            $"__lock:{compositeKey}",
            entry =>
            {
                // TTL court : le GC libère automatiquement les verrous après 30 s
                // Évite la fuite mémoire d'un ConcurrentDictionary non borné
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                // Taille requise quand SizeLimit est configuré sur l'IMemoryCache
                entry.Size = 1;
                return new SemaphoreSlim(1, 1);
            })!;
}
