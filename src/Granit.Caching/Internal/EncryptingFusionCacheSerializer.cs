using Granit.Caching.Options;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace Granit.Caching.Internal;

/// <summary>
/// Decorator that applies AES-256-GCM encryption on top of an inner <see cref="IFusionCacheSerializer"/>.
/// Used as the FusionCache serializer for the distributed L2 cache (Redis).
/// </summary>
/// <remarks>
/// <para>
/// Encryption is applied <b>only to L2 traffic</b> (serialize for Redis / deserialize from Redis).
/// The L1 in-memory cache stores plain .NET objects — same threat model as <c>IMemoryCache</c>.
/// </para>
/// <para>
/// Per-type encryption is resolved by <see cref="CacheEncryptionResolver"/>:
/// types decorated with <see cref="CacheEncryptedAttribute"/> override the global
/// <see cref="CachingOptions.EncryptValues"/> flag.
/// </para>
/// </remarks>
internal sealed class EncryptingFusionCacheSerializer(
    IFusionCacheSerializer inner,
    ICacheValueEncryptor encryptor,
    CachingOptions options) : IFusionCacheSerializer
{
    private readonly IFusionCacheSerializer _inner = inner;
    private readonly ICacheValueEncryptor _encryptor = encryptor;
    private readonly CachingOptions _options = options;

    /// <inheritdoc/>
    public byte[] Serialize<T>(T? obj)
    {
        byte[] serialized = _inner.Serialize(obj);
        return CacheEncryptionResolver.ShouldEncrypt(typeof(T), _options)
            ? _encryptor.Encrypt(serialized)
            : serialized;
    }

    /// <inheritdoc/>
    public T? Deserialize<T>(byte[] data)
    {
        byte[] plain = CacheEncryptionResolver.ShouldEncrypt(typeof(T), _options)
            ? _encryptor.Decrypt(data)
            : data;
        return _inner.Deserialize<T>(plain);
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
    {
        byte[] serialized = await _inner.SerializeAsync(obj, token).ConfigureAwait(false);
        return CacheEncryptionResolver.ShouldEncrypt(typeof(T), _options)
            ? _encryptor.Encrypt(serialized)
            : serialized;
    }

    /// <inheritdoc/>
    public async ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
    {
        byte[] plain = CacheEncryptionResolver.ShouldEncrypt(typeof(T), _options)
            ? _encryptor.Decrypt(data)
            : data;
        return await _inner.DeserializeAsync<T>(plain, token).ConfigureAwait(false);
    }
}
