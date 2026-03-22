using System.Collections.Concurrent;
using System.Security.Cryptography;
using Granit.Encryption;
using Granit.Vault.HashiCorp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.Commons;

namespace Granit.Vault.HashiCorp.Services;

/// <summary>
/// Implementation of <see cref="IEntityEncryptionKeyStore"/> backed by HashiCorp Vault KV v2.
/// </summary>
/// <remarks>
/// <para>
/// Key path format: <c>{kvMountPoint}/data/granit/encryption/isolated/{entityType}/{entityId}</c>.
/// The secret value is a 32-byte AES-256 key stored as Base64 in the <c>"key"</c> field.
/// </para>
/// <para>
/// Includes an in-memory cache to avoid round-trips to Vault on every entity materialization.
/// Cache entries are evicted on <see cref="DeleteKeyAsync"/> (crypto-shredding).
/// </para>
/// </remarks>
public sealed partial class HashiCorpEntityEncryptionKeyStore(
    IVaultClient vaultClient,
    IOptions<HashiCorpVaultOptions> options,
    ILogger<HashiCorpEntityEncryptionKeyStore> logger) : IEntityEncryptionKeyStore
{
    private const int KeySizeBytes = 32; // AES-256
    private const string SecretKeyField = "key";
    private const string PathPrefix = "granit/encryption/isolated";

    private readonly string _kvMountPoint = options.Value.KvMountPoint;
    private readonly ConcurrentDictionary<string, byte[]> _cache = new(StringComparer.Ordinal);

    public async Task<byte[]> GetOrCreateKeyAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildCacheKey(entityType, entityId);

        if (_cache.TryGetValue(cacheKey, out byte[]? cached))
        {
            return cached;
        }

        string path = BuildPath(entityType, entityId);

        // Try to read existing key
        try
        {
            Secret<SecretData> secret = await vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path, mountPoint: _kvMountPoint)
                .WaitAsync(cancellationToken).ConfigureAwait(false);

            if (secret.Data.Data.TryGetValue(SecretKeyField, out object? value) &&
                value is string base64Key)
            {
                byte[] existingKey = Convert.FromBase64String(base64Key);
                _cache.TryAdd(cacheKey, existingKey);
                return existingKey;
            }
        }
        catch (VaultSharp.Core.VaultApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Key doesn't exist yet — create below
        }

        // Generate new key
        byte[] newKey = RandomNumberGenerator.GetBytes(KeySizeBytes);
        string newKeyBase64 = Convert.ToBase64String(newKey);

        await vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
            path,
            new Dictionary<string, object> { [SecretKeyField] = newKeyBase64 },
            mountPoint: _kvMountPoint)
            .WaitAsync(cancellationToken).ConfigureAwait(false);

        _cache.TryAdd(cacheKey, newKey);
        LogKeyCreated(logger, entityType, entityId);
        return newKey;
    }

    public async Task<byte[]?> GetKeyAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildCacheKey(entityType, entityId);

        if (_cache.TryGetValue(cacheKey, out byte[]? cached))
        {
            return cached;
        }

        string path = BuildPath(entityType, entityId);

        try
        {
            Secret<SecretData> secret = await vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path, mountPoint: _kvMountPoint)
                .WaitAsync(cancellationToken).ConfigureAwait(false);

            if (secret.Data.Data.TryGetValue(SecretKeyField, out object? value) &&
                value is string base64Key)
            {
                byte[] key = Convert.FromBase64String(base64Key);
                _cache.TryAdd(cacheKey, key);
                return key;
            }
        }
        catch (VaultSharp.Core.VaultApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Key doesn't exist (possibly shredded)
        }

        return null;
    }

    public async Task DeleteKeyAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        string path = BuildPath(entityType, entityId);
        string cacheKey = BuildCacheKey(entityType, entityId);

        // Permanently destroy all versions — irreversible crypto-shredding
        await vaultClient.V1.Secrets.KeyValue.V2
            .DeleteMetadataAsync(path, mountPoint: _kvMountPoint)
            .WaitAsync(cancellationToken).ConfigureAwait(false);

        _cache.TryRemove(cacheKey, out _);
        LogKeyDeleted(logger, entityType, entityId);
    }

    public async Task<bool> KeyExistsAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildCacheKey(entityType, entityId);

        if (_cache.ContainsKey(cacheKey))
        {
            return true;
        }

        byte[]? key = await GetKeyAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);
        return key is not null;
    }

    private static string BuildPath(string entityType, string entityId) =>
        $"{PathPrefix}/{entityType}/{entityId}";

    private static string BuildCacheKey(string entityType, string entityId) =>
        $"{entityType}:{entityId}";

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Per-entity encryption key created in Vault KV for {EntityType}/{EntityId}")]
    private static partial void LogKeyCreated(ILogger logger, string entityType, string entityId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Per-entity encryption key permanently destroyed in Vault KV for {EntityType}/{EntityId}")]
    private static partial void LogKeyDeleted(ILogger logger, string entityType, string entityId);
}
