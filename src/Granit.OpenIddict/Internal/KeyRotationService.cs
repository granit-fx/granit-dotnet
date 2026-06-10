using System.Security.Cryptography;
using Granit.Encryption;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.Internal;

/// <summary>
/// <see cref="IKeyRotationService"/> implementation that manages the full signing key lifecycle.
/// </summary>
#pragma warning disable GRSEC003 // Key material management, not secrets
internal sealed partial class KeyRotationService(
    ISigningKeyStore keyStore,
    IStringEncryptionService encryptionService,
    IOptions<GranitKeyRotationOptions> rotationOptions,
    TimeProvider timeProvider,
    ILogger<KeyRotationService> logger) : IKeyRotationService
{
    /// <inheritdoc/>
    public async Task<KeyRotationResult> RotateAsync(CancellationToken cancellationToken = default)
    {
        GranitKeyRotationOptions options = rotationOptions.Value;

        if (!options.Enabled)
        {
            Log.KeyRotationDisabled(logger);
            return new KeyRotationResult(0, 0, 0, 0);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        int generated = 0;
        int retired = 0;
        int revoked = 0;

        // 1. Ensure active signing key exists, rotate if expiring soon
        (int g, int r) = await EnsureActiveKeyAsync("signing", options, now, cancellationToken).ConfigureAwait(false);
        generated += g;
        retired += r;

        // 2. Ensure active encryption key exists, rotate if expiring soon
        (g, r) = await EnsureActiveKeyAsync("encryption", options, now, cancellationToken).ConfigureAwait(false);
        generated += g;
        retired += r;

        // 3. Revoke keys whose grace period has expired
        IReadOnlyList<SigningKey> retiredKeys = await keyStore
            .GetKeysAsync([SigningKeyStatus.Retired], cancellationToken).ConfigureAwait(false);

        foreach (SigningKey retiredKey in retiredKeys.Where(k =>
            k.RetiredAt.HasValue && now - k.RetiredAt.Value > options.GracePeriod))
        {
            retiredKey.Revoke();

            // A concurrent rotation may already have revoked this key — skip on a lost race.
            if (!await keyStore.UpdateAsync(retiredKey, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            Log.KeyRevoked(logger, retiredKey.KeyId);
            revoked++;
        }

        // 4. Prune revoked keys older than 30 days
        int pruned = await keyStore
            .PruneRevokedAsync(now.AddDays(-30), cancellationToken).ConfigureAwait(false);

        if (pruned > 0)
        {
            Log.KeysPruned(logger, pruned);
        }

        return new KeyRotationResult(generated, retired, revoked, pruned);
    }

    /// <summary>
    /// Ensures an active key exists for <paramref name="keyType"/>, rotating it when it nears
    /// expiry. Returns the number of keys generated and retired in this cycle.
    /// </summary>
    /// <remarks>
    /// Rotation retires the current key <b>before</b> generating its replacement. The retire is
    /// an optimistic-concurrency update on <see cref="SigningKey.ConcurrencyStamp"/>: when two
    /// rotations race (cron + manual trigger, message redelivery, or per-replica scheduling), only
    /// one wins the retire and proceeds to generate. The loser observes a lost race and aborts,
    /// guaranteeing a single new active key per type instead of two.
    /// </remarks>
    private async Task<(int Generated, int Retired)> EnsureActiveKeyAsync(
        string keyType,
        GranitKeyRotationOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        SigningKey? activeKey = await keyStore
            .GetActiveKeyAsync(keyType, cancellationToken).ConfigureAwait(false);

        if (activeKey is null)
        {
            await GenerateKeyAsync(keyType, options, now, cancellationToken).ConfigureAwait(false);
            return (1, 0);
        }

        if (activeKey.ExpiresAt - now <= options.RotationLeadTime)
        {
            Log.KeyRotationStarted(logger, activeKey.KeyId, activeKey.ExpiresAt);

            // Retire first: this is the concurrency gate. If a competing rotation already
            // retired this key, our update loses the race and we MUST NOT generate a duplicate.
            activeKey.Retire(now);
            if (!await keyStore.UpdateAsync(activeKey, cancellationToken).ConfigureAwait(false))
            {
                Log.ConcurrentRotationSkipped(logger, activeKey.KeyId);
                return (0, 0);
            }

            Log.KeyRetired(logger, activeKey.KeyId);

            await GenerateKeyAsync(keyType, options, now, cancellationToken).ConfigureAwait(false);

            return (1, 1);
        }

        return (0, 0);
    }

    private async Task GenerateKeyAsync(
        string keyType,
        GranitKeyRotationOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var rsa = RSA.Create(options.RsaKeySize);
        byte[] privateKey = rsa.ExportRSAPrivateKey();

        try
        {
#pragma warning disable GRSEC002 // Key ID suffix, not a database entity ID
            string keyId = $"{keyType}-{now:yyyyMMdd}-{Guid.NewGuid():N}";
#pragma warning restore GRSEC002

            string algorithm = keyType == "signing"
                ? options.SigningAlgorithm
                : "RSA-OAEP";

            var newKey = SigningKey.Create(
                keyId,
                keyType,
                algorithm,
                encryptionService.Encrypt(Convert.ToBase64String(privateKey)),
                activatedAt: now,
                expiresAt: now + options.KeyLifetime,
                keySize: options.RsaKeySize);

            await keyStore.CreateAsync(newKey, cancellationToken).ConfigureAwait(false);
            Log.KeyGenerated(logger, keyId, keyType, options.RsaKeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Key rotation disabled.")]
        public static partial void KeyRotationDisabled(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Key rotation started for {KeyId} (expires {ExpiresAt}).")]
        public static partial void KeyRotationStarted(ILogger logger, string keyId, DateTimeOffset expiresAt);

        [LoggerMessage(Level = LogLevel.Information, Message = "Generated new {KeyType} key: {KeyId} (RSA-{KeySize}).")]
        public static partial void KeyGenerated(ILogger logger, string keyId, string keyType, int keySize);

        [LoggerMessage(Level = LogLevel.Information, Message = "Key {KeyId} retired (grace period started).")]
        public static partial void KeyRetired(ILogger logger, string keyId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Key rotation for {KeyId} skipped — a concurrent rotation already retired it.")]
        public static partial void ConcurrentRotationSkipped(ILogger logger, string keyId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Key {KeyId} revoked (grace period expired).")]
        public static partial void KeyRevoked(ILogger logger, string keyId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Pruned {Count} revoked keys.")]
        public static partial void KeysPruned(ILogger logger, int count);
    }
}
#pragma warning restore GRSEC003
