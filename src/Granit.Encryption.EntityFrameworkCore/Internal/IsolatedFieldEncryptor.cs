using System.Security.Cryptography;
using System.Text;

namespace Granit.Encryption.EntityFrameworkCore.Internal;

/// <summary>
/// AES-256-CBC + HMAC-SHA256 encrypt/decrypt using a per-entity key.
/// Format: <c>Base64(IV[16] || CipherText || HMAC-SHA256(IV || CipherText)[32])</c>.
/// </summary>
/// <remarks>
/// Same encrypt-then-MAC scheme as <c>AesStringEncryptionProvider</c>, but uses
/// a raw 32-byte key instead of PBKDF2-derived key material. The key is split:
/// first 32 bytes for AES-256, HMAC key derived via HKDF from the same key material.
/// </remarks>
internal static class IsolatedFieldEncryptor
{
    private const int IvSize = 16;
    private const int HmacSize = 32;
    private const int AesKeySize = 32;
    private static readonly byte[] HkdfInfo = "granit-isolated-hmac"u8.ToArray();

    /// <summary>Encrypts plaintext with the given per-entity key.</summary>
    internal static string Encrypt(byte[] key, string plainText)
    {
        ValidateKey(key);

        byte[] hmacKey = DeriveHmacKey(key);
        byte[] iv = RandomNumberGenerator.GetBytes(IvSize);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Encrypt-then-MAC: HMAC-SHA256(IV || CipherText)
        int dataLength = IvSize + cipherBytes.Length;
        byte[] dataToMac = new byte[dataLength];
        Buffer.BlockCopy(iv, 0, dataToMac, 0, IvSize);
        Buffer.BlockCopy(cipherBytes, 0, dataToMac, IvSize, cipherBytes.Length);
        byte[] hmac = HMACSHA256.HashData(hmacKey, dataToMac);

        // Format: IV[16] || CipherText || HMAC[32]
        byte[] output = new byte[dataLength + HmacSize];
        Buffer.BlockCopy(dataToMac, 0, output, 0, dataLength);
        Buffer.BlockCopy(hmac, 0, output, dataLength, HmacSize);

        return Convert.ToBase64String(output);
    }

    /// <summary>
    /// Decrypts ciphertext with the given per-entity key.
    /// Returns <c>null</c> if the ciphertext is invalid, tampered, or the key is wrong.
    /// </summary>
    internal static string? Decrypt(byte[] key, string cipherText)
    {
        ValidateKey(key);

        if (string.IsNullOrEmpty(cipherText))
        {
            return null;
        }

        byte[] input;
        try
        {
            input = Convert.FromBase64String(cipherText);
        }
        catch (FormatException)
        {
            return null;
        }

        // Minimum: IV[16] + one AES block[16] + HMAC[32] = 64 bytes
        if (input.Length < IvSize + 16 + HmacSize)
        {
            return null;
        }

        byte[] hmacKey = DeriveHmacKey(key);

        // Verify HMAC before decryption (encrypt-then-MAC: always verify first)
        byte[] receivedHmac = input[^HmacSize..];
        byte[] dataToMac = input[..^HmacSize];
        byte[] computedHmac = HMACSHA256.HashData(hmacKey, dataToMac);

        if (!CryptographicOperations.FixedTimeEquals(computedHmac, receivedHmac))
        {
            return null;
        }

        byte[] iv = input[..IvSize];
        byte[] cipherBytes = input[IvSize..^HmacSize];

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        try
        {
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static byte[] DeriveHmacKey(byte[] key) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, key, HmacSize, info: HkdfInfo);

    private static void ValidateKey(byte[] key)
    {
        if (key.Length != AesKeySize)
        {
            throw new ArgumentException(
                $"Per-entity encryption key must be {AesKeySize} bytes (AES-256), got {key.Length}.",
                nameof(key));
        }
    }
}
