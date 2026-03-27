using System.Security.Cryptography;
using Granit.Caching.Options;
using Microsoft.Extensions.Options;

namespace Granit.Caching;

/// <summary>
/// AES-256-GCM implementation of <see cref="ICacheValueEncryptor"/> for ISO 27001 compliance.
/// </summary>
/// <remarks>
/// Security characteristics:
/// <list type="bullet">
///   <item>Algorithm: AES-256-GCM (authenticated encryption — tamper detection via 128-bit tag)</item>
///   <item>Nonce: 12 bytes generated randomly by <see cref="RandomNumberGenerator.Fill"/> for each <see cref="Encrypt"/> call</item>
///   <item>Tag: 16 bytes (128-bit authentication tag, verified on <see cref="Decrypt"/>)</item>
///   <item>Ciphertext format: <c>[12 bytes Nonce][16 bytes Tag][N bytes CipherText]</c></item>
///   <item>Key: 256 bits (32 bytes) provided as base64 via <c>Cache:Encryption:Key</c></item>
/// </list>
/// The AES key must be provided exclusively via Vault or secure configuration.
/// Never store the key in plaintext in code or committed configuration files.
/// </remarks>
public sealed class AesCacheValueEncryptor(IOptions<CacheEncryptionOptions> options) : ICacheValueEncryptor, IDisposable
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int KeySizeBits = 256;
    private const int KeySizeBytes = KeySizeBits / 8;
    private const int OverheadBytes = NonceSizeBytes + TagSizeBytes;

    private readonly byte[] _key = ParseAndValidateKey(options.Value.Key);

    private static byte[] ParseAndValidateKey(string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            throw new InvalidOperationException(
                "AES key (Cache:Encryption:Key) is required for cache encryption. " +
                "Provide it via Vault or environment variables.");
        }

        byte[] key = Convert.FromBase64String(base64Key);

        if (key.Length != KeySizeBytes)
        {
            throw new ArgumentException(
                $"AES key must be {KeySizeBits} bits ({KeySizeBytes} bytes). " +
                $"Received: {key.Length * 8} bits ({key.Length} bytes).");
        }

        if (CryptographicOperations.FixedTimeEquals(key, new byte[KeySizeBytes]))
        {
            throw new ArgumentException(
                "AES key is all zeros — provide a cryptographically random key via Vault.");
        }

        return key;
    }

    /// <summary>
    /// Encrypts the provided data using AES-256-GCM with a random nonce.
    /// </summary>
    /// <param name="plaintext">The plaintext data.</param>
    /// <returns>Data in the format <c>[12 bytes Nonce][16 bytes Tag][N bytes CipherText]</c>.</returns>
    public byte[] Encrypt(byte[] plaintext)
    {
        byte[] nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        byte[] result = new byte[OverheadBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, OverheadBytes, ciphertext.Length);

        return result;
    }

    /// <summary>
    /// Decrypts data in the format <c>[12 bytes Nonce][16 bytes Tag][N bytes CipherText]</c>.
    /// Throws <see cref="CryptographicException"/> if the authentication tag is invalid (tampered data).
    /// </summary>
    /// <param name="ciphertext">The encrypted data.</param>
    /// <returns>The decrypted data.</returns>
    /// <exception cref="ArgumentException">Thrown when the ciphertext is too short.</exception>
    /// <exception cref="CryptographicException">Thrown when the authentication tag is invalid (tampered data).</exception>
    public byte[] Decrypt(byte[] ciphertext)
    {
        if (ciphertext.Length < OverheadBytes)
        {
            throw new ArgumentException(
                $"Invalid ciphertext: minimum {OverheadBytes} bytes required (nonce + tag), " +
                $"received {ciphertext.Length} bytes.");
        }

        byte[] nonce = new byte[NonceSizeBytes];
        Buffer.BlockCopy(ciphertext, 0, nonce, 0, NonceSizeBytes);

        byte[] tag = new byte[TagSizeBytes];
        Buffer.BlockCopy(ciphertext, NonceSizeBytes, tag, 0, TagSizeBytes);

        int cipherLength = ciphertext.Length - OverheadBytes;
        byte[] cipher = new byte[cipherLength];
        Buffer.BlockCopy(ciphertext, OverheadBytes, cipher, 0, cipherLength);

        byte[] plaintext = new byte[cipherLength];
        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Decrypt(nonce, cipher, tag, plaintext);

        return plaintext;
    }

    /// <inheritdoc/>
    public void Dispose() => CryptographicOperations.ZeroMemory(_key);
}
