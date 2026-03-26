using Granit.Caching.Options;
namespace Granit.Caching;

/// <summary>
/// Encrypts and decrypts serialized values before they are stored in <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>.
/// </summary>
/// <remarks>
/// Two implementations are provided:
/// <list type="bullet">
///   <item><see cref="NullCacheValueEncryptor"/> — no-op, used in development with the Memory provider.</item>
///   <item><see cref="AesCacheValueEncryptor"/> — AES-256-GCM (authenticated encryption) with a random nonce, used in production with Redis.</item>
/// </list>
/// Encryption is enabled per type via <see cref="CacheEncryptedAttribute"/> or globally via
/// <see cref="CachingOptions.EncryptValues"/>.
/// </remarks>
public interface ICacheValueEncryptor
{
    /// <summary>
    /// Encrypts the provided bytes. Generates a random nonce per call (AES-256-GCM).
    /// </summary>
    /// <param name="plaintext">Plaintext data (serialized JSON).</param>
    /// <returns>Encrypted data in the format <c>[12 bytes Nonce][16 bytes Tag][N bytes CipherText]</c>.</returns>
    byte[] Encrypt(byte[] plaintext);

    /// <summary>
    /// Decrypts the provided bytes in the format <c>[12 bytes Nonce][16 bytes Tag][N bytes CipherText]</c>.
    /// Throws <see cref="System.Security.Cryptography.CryptographicException"/> if the data has been tampered with.
    /// </summary>
    /// <param name="ciphertext">Encrypted data.</param>
    /// <returns>Decrypted data (serialized JSON).</returns>
    byte[] Decrypt(byte[] ciphertext);
}
