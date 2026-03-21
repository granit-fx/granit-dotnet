using ZiggyCreatures.Caching.Fusion.Serialization;

namespace Granit.Caching.Internal;

/// <summary>
/// Decorator that applies AES-256-CBC encryption on top of an inner <see cref="IFusionCacheSerializer"/>.
/// Used as the FusionCache serializer for the distributed L2 cache (Redis).
/// </summary>
/// <remarks>
/// <para>
/// Encryption is applied <b>only to L2 traffic</b> (serialize for Redis / deserialize from Redis).
/// The L1 in-memory cache stores plain .NET objects — same threat model as <c>IMemoryCache</c>.
/// </para>
/// <para>
/// When <paramref name="encrypt"/> is <c>false</c>, the decorator is a transparent pass-through.
/// </para>
/// </remarks>
internal sealed class EncryptingFusionCacheSerializer(
    IFusionCacheSerializer inner,
    ICacheValueEncryptor encryptor,
    bool encrypt) : IFusionCacheSerializer
{
    private readonly IFusionCacheSerializer _inner = inner;
    private readonly ICacheValueEncryptor _encryptor = encryptor;
    private readonly bool _encrypt = encrypt;

    /// <inheritdoc/>
    public byte[] Serialize<T>(T? obj)
    {
        byte[] serialized = _inner.Serialize(obj);
        return _encrypt ? _encryptor.Encrypt(serialized) : serialized;
    }

    /// <inheritdoc/>
    public T? Deserialize<T>(byte[] data)
    {
        byte[] plain = _encrypt ? _encryptor.Decrypt(data) : data;
        return _inner.Deserialize<T>(plain);
    }

    /// <inheritdoc/>
    public ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token = default)
    {
        byte[] serialized = _inner.Serialize(obj);
        byte[] result = _encrypt ? _encryptor.Encrypt(serialized) : serialized;
        return new ValueTask<byte[]>(result);
    }

    /// <inheritdoc/>
    public ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token = default)
    {
        byte[] plain = _encrypt ? _encryptor.Decrypt(data) : data;
        T? result = _inner.Deserialize<T>(plain);
        return new ValueTask<T?>(result);
    }
}
