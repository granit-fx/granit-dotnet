using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;

namespace Granit.Encryption.Services;

/// <summary>
/// In-memory implementation of <see cref="IEntityEncryptionKeyStore"/> for development
/// and testing environments where a Vault provider is not available.
/// </summary>
/// <remarks>
/// Keys are stored in a <see cref="ConcurrentDictionary{TKey,TValue}"/> and are lost
/// when the process restarts. This implementation MUST NOT be used in production —
/// a secure provider (e.g., HashiCorp Vault) should override this registration.
/// The constructor fails fast outside the Development environment, so a host that forgets
/// to register a durable key store cannot silently run with volatile keys.
/// </remarks>
internal sealed class InMemoryEntityEncryptionKeyStore : IEntityEncryptionKeyStore
{
    private const int KeySizeBytes = 32; // AES-256

    private readonly ConcurrentDictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    public InMemoryEntityEncryptionKeyStore(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        // Volatile keys are lost on every restart, which corrupts previously encrypted data and
        // makes GDPR crypto-shredding non-durable. Refuse to resolve outside Development so the
        // misconfiguration surfaces at startup instead of as silent PII corruption in production.
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{nameof(InMemoryEntityEncryptionKeyStore)} is a development-only fallback and is forbidden " +
                $"outside the Development environment (current: '{environment.EnvironmentName}'). " +
                "Its keys are held in memory and lost on every restart, corrupting previously encrypted data " +
                "and leaving GDPR crypto-shredding without a durable guarantee. " +
                $"Register a Vault-backed {nameof(IEntityEncryptionKeyStore)} for production use.");
        }
    }

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
        if (_keys.TryRemove(cacheKey, out byte[]? key))
        {
            CryptographicOperations.ZeroMemory(key);
        }

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
