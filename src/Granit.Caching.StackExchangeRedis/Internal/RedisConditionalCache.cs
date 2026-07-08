using System.Text.Json;
using Granit.Caching.Internal;
using Granit.Caching.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Granit.Caching.StackExchangeRedis.Internal;

/// <summary>
/// Redis-backed implementation of <see cref="IConditionalCache"/> using atomic
/// SET NX PX / SET XX PX commands. Values are JSON-serialized and optionally
/// encrypted via <see cref="ICacheValueEncryptor"/>.
/// </summary>
/// <remarks>
/// Keys are namespaced by <see cref="ConditionalCacheKeyComposer"/> (app prefix + tenant
/// segment). Raw <see cref="IDatabase"/> writes bypass the <c>InstanceName</c> prefix that
/// <c>RedisCache</c> applies to <c>IDistributedCache</c> entries — without composition,
/// conditional keys would land at the Redis root and two applications sharing an instance
/// could collide on identical logical keys.
/// </remarks>
internal sealed class RedisConditionalCache(
    IConnectionMultiplexer redis,
    ICacheValueEncryptor encryptor,
    IOptions<CachingOptions> cachingOptions,
    ConditionalCacheKeyComposer keyComposer) : IConditionalCache
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly CachingOptions _options = cachingOptions.Value;

    /// <inheritdoc/>
    public async Task<bool> SetIfAbsentAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        RedisValue payload = Serialize(value);
        return await _db.StringSetAsync(keyComposer.Compose(key), payload, ttl, When.NotExists)
            .WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> SetIfPresentAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        RedisValue payload = Serialize(value);
        return await _db.StringSetAsync(keyComposer.Compose(key), payload, ttl, When.Exists)
            .WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        RedisValue raw = await _db.StringGetAsync(keyComposer.Compose(key))
            .WaitAsync(cancellationToken).ConfigureAwait(false);

        return raw.IsNullOrEmpty ? default : Deserialize<T>(raw);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await _db.KeyDeleteAsync(keyComposer.Compose(key)).WaitAsync(cancellationToken).ConfigureAwait(false);

    private RedisValue Serialize<T>(T value)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, _options.JsonOptions);
        return CacheEncryptionResolver.ShouldEncrypt(typeof(T), _options)
            ? encryptor.Encrypt(json)
            : json;
    }

    private T? Deserialize<T>(RedisValue raw)
    {
        byte[]? bytes = (byte[]?)raw;
        if (bytes is null)
        {
            return default;
        }

        byte[] json = CacheEncryptionResolver.ShouldEncrypt(typeof(T), _options)
            ? encryptor.Decrypt(bytes)
            : bytes;
        return JsonSerializer.Deserialize<T>(json, _options.JsonOptions);
    }
}
