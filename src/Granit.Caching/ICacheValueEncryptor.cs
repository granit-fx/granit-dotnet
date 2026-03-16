using Granit.Caching.Options;
namespace Granit.Caching;

/// <summary>
/// Encrypts and decrypts serialized values before they are stored in <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>.
/// </summary>
/// <remarks>
/// Two implementations are provided:
/// <list type="bullet">
///   <item><see cref="NullCacheValueEncryptor"/> — no-op, used in development with the Memory provider.</item>
///   <item><see cref="AesCacheValueEncryptor"/> — AES-256-CBC with a random IV, used in production with Redis.</item>
/// </list>
/// Encryption is enabled per type via <see cref="CacheEncryptedAttribute"/> or globally via
/// <see cref="CachingOptions.EncryptValues"/>.
/// </remarks>
public interface ICacheValueEncryptor
{
    /// <summary>
    /// Encrypts the provided bytes. Generates a random IV per call (AES-256-CBC).
    /// </summary>
    /// <param name="plaintext">Plaintext data (serialized JSON).</param>
    /// <returns>Encrypted data in the format <c>[16 bytes IV][N bytes CipherText]</c>.</returns>
    byte[] Encrypt(byte[] plaintext);

    /// <summary>
    /// Decrypts the provided bytes in the format <c>[16 bytes IV][N bytes CipherText]</c>.
    /// </summary>
    /// <param name="ciphertext">Encrypted data.</param>
    /// <returns>Decrypted data (serialized JSON).</returns>
    byte[] Decrypt(byte[] ciphertext);
}
