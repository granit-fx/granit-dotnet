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
public sealed class AesCacheValueEncryptor : ICacheValueEncryptor
{
    private const int IvSizeBytes = 16;
    private const int KeySizeBits = 256;
    private const int KeySizeBytes = KeySizeBits / 8;

    private readonly byte[] _key;

    /// <param name="options">Encryption options containing the AES key in base64.</param>
    /// <exception cref="InvalidOperationException">Thrown when the key is missing from configuration.</exception>
    /// <exception cref="ArgumentException">Thrown when the key is not 256 bits (32 bytes).</exception>
    public AesCacheValueEncryptor(IOptions<CacheEncryptionOptions> options)
    {
        string? base64Key = options.Value.Key;

        if (string.IsNullOrWhiteSpace(base64Key))
        {
            throw new InvalidOperationException(
                "La clé AES (Cache:Encryption:Key) est requise pour le chiffrement du cache. " +
                "Fournissez-la via Vault ou les variables d'environnement.");
        }

        _key = Convert.FromBase64String(base64Key);

        if (_key.Length != KeySizeBytes)
        {
            throw new ArgumentException(
                $"La clé AES doit être de {KeySizeBits} bits ({KeySizeBytes} bytes). " +
                $"Longueur reçue : {_key.Length * 8} bits ({_key.Length} bytes).");
        }
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
                $"Le ciphertext est invalide : minimum {IvSizeBytes} bytes requis (IV), " +
                $"reçu {ciphertext.Length} bytes.");
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
