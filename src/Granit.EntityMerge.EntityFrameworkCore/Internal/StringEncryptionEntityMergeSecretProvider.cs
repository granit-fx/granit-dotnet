using System.Security.Cryptography;
using System.Text;
using Granit.Encryption;

namespace Granit.EntityMerge.EntityFrameworkCore.Internal;

/// <summary>
/// Default <see cref="IEntityMergeSecretProvider"/> — derives the MAC key by SHA-256 hashing
/// the ciphertext of a stable constant under <see cref="IStringEncryptionService"/>. The
/// derived key is therefore deterministic per deployment (so all hosts in a cluster compute
/// the same MAC) but cannot be recovered by an attacker who lacks the encryption key.
/// Cached on first use — derivation is one-shot, deterministic, and side-effect-free.
/// </summary>
internal sealed class StringEncryptionEntityMergeSecretProvider(IStringEncryptionService stringEncryption)
    : IEntityMergeSecretProvider
{
    // Stable constant: derives a single deployment-bound MAC key. Bumping the suffix forces
    // re-derivation if the framework ever needs to invalidate previously cached entries
    // (consumer migration would be: drop the merge_idempotency rows, restart).
    private const string Constant = "granit.mergeable.idempotency-mac:v1";

    private readonly Lock _lock = new();
    private byte[]? _cachedKey;

    public byte[] GetMacKey()
    {
        if (_cachedKey is not null)
        {
            return _cachedKey;
        }

        lock (_lock)
        {
            if (_cachedKey is not null)
            {
                return _cachedKey;
            }

            string ciphertext = stringEncryption.Encrypt(Constant);
            // SHA-256 → 32 bytes, the canonical key length for HMAC-SHA-256 (RFC 2104 §3).
            _cachedKey = SHA256.HashData(Encoding.UTF8.GetBytes(ciphertext));
            return _cachedKey;
        }
    }
}
