using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Granit.Encryption.Services;

/// <summary>
/// In-memory implementation of <see cref="IEntityEncryptionKeyStore"/> for development
/// and testing environments where a Vault provider is not available.
/// </summary>
/// <remarks>
/// Keys are stored in a <see cref="ConcurrentDictionary{TKey,TValue}"/> and are lost
/// when the process restarts. This implementation MUST NOT be used in production —
/// a secure provider (e.g., HashiCorp Vault) should override this registration.
/// </remarks>
internal sealed class InMemoryEntityEncryptionKeyStore : IEntityEncryptionKeyStore
{
    private const int KeySizeBytes = 32; // AES-256

    private readonly ConcurrentDictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<byte[]> GetOrCreateKeyAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildKey(entityType, entityId);
        byte[] key = _keys.GetOrAdd(cacheKey, _ => RandomNumberGenerator.GetBytes(KeySizeBytes));
        return Task.FromResult(key);
    }

    /// <inheritdoc/>
    public Task<byte[]?> GetKeyAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildKey(entityType, entityId);
        byte[]? result = _keys.TryGetValue(cacheKey, out byte[]? key) ? key : null;
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task DeleteKeyAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildKey(entityType, entityId);
        _keys.TryRemove(cacheKey, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> KeyExistsAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildKey(entityType, entityId);
        return Task.FromResult(_keys.ContainsKey(cacheKey));
    }

    private static string BuildKey(string entityType, string entityId) =>
        $"{entityType}:{entityId}";
}
