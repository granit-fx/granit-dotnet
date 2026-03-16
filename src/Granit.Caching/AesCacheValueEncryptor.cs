using System.Security.Cryptography;
using Granit.Caching.Options;
using Microsoft.Extensions.Options;

namespace Granit.Caching;

/// <summary>
/// AES-256-CBC implementation of <see cref="ICacheValueEncryptor"/> for ISO 27001 compliance.
/// </summary>
/// <remarks>
/// Security characteristics:
/// <list type="bullet">
///   <item>Algorithm: AES-256-CBC via <see cref="Aes.Create()"/></item>
///   <item>IV: 16 bytes generated randomly by <see cref="RandomNumberGenerator.Fill"/> for each <see cref="Encrypt"/> call</item>
///   <item>Ciphertext format: <c>[16 bytes IV][N bytes CipherText]</c></item>
///   <item>Key: 256 bits (32 bytes) provided as base64 via <c>Cache:Encryption:Key</c></item>
/// </list>
/// The AES key must be provided exclusively via Vault or secure configuration.
/// Never store the key in plaintext in code or committed configuration files.
/// </remarks>
public sealed class AesCacheValueEncryptor(IOptions<CacheEncryptionOptions> options) : ICacheValueEncryptor
{
    private const int IvSizeBytes = 16;
    private const int KeySizeBits = 256;
    private const int KeySizeBytes = KeySizeBits / 8;

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

        return key;
    }

    /// <summary>
    /// Encrypts the provided data using AES-256-CBC with a random IV.
    /// </summary>
    /// <param name="plaintext">The plaintext data.</param>
    /// <returns>Data in the format <c>[16 bytes IV][N bytes CipherText]</c>.</returns>
    public byte[] Encrypt(byte[] plaintext)
    {
        byte[] iv = new byte[IvSizeBytes];
        RandomNumberGenerator.Fill(iv);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        byte[] result = new byte[IvSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(iv, 0, result, 0, IvSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, IvSizeBytes, ciphertext.Length);

        return result;
    }

    /// <summary>
    /// Decrypts data in the format <c>[16 bytes IV][N bytes CipherText]</c>.
    /// </summary>
    /// <param name="ciphertext">The encrypted data.</param>
    /// <returns>The decrypted data.</returns>
    /// <exception cref="ArgumentException">Thrown when the ciphertext is too short (fewer than 16 bytes).</exception>
    public byte[] Decrypt(byte[] ciphertext)
    {
        if (ciphertext.Length < IvSizeBytes)
        {
            throw new ArgumentException(
                $"Invalid ciphertext: minimum {IvSizeBytes} bytes required (IV), " +
                $"received {ciphertext.Length} bytes.");
        }

        byte[] iv = new byte[IvSizeBytes];
        Buffer.BlockCopy(ciphertext, 0, iv, 0, IvSizeBytes);

        int cipherLength = ciphertext.Length - IvSizeBytes;
        byte[] cipher = new byte[cipherLength];
        Buffer.BlockCopy(ciphertext, IvSizeBytes, cipher, 0, cipherLength);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
    }
}
