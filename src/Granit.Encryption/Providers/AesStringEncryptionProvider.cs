using System.Security.Cryptography;
using System.Text;
using Granit.Encryption;
using Granit.Encryption.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Encryption.Providers;

/// <summary>
/// AES-256-CBC + HMAC-SHA256 provider with PBKDF2 key derivation (encrypt-then-MAC).
/// <para>
/// Output format: <c>Base64(IV[16] || CipherText || HMAC-SHA256(IV || CipherText)[32])</c>.
/// </para>
/// <para>
/// The HMAC tag guarantees ciphertext integrity and authenticity, preventing
/// padding oracle attacks and silent data corruption (ISO 27001 requirement).
/// </para>
/// Designed for frequent operations (&lt; 1 ms after startup).
/// </summary>
public sealed partial class AesStringEncryptionProvider : IStringEncryptionProvider, IDisposable
{
    /// <remarks>
    /// SECURITY: Fixed internal salt for PBKDF2 key derivation.
    /// This is acceptable because the PassPhrase MUST come from Vault (high entropy, 256-bit minimum).
    /// The salt's purpose is to prevent rainbow table attacks on weak PassPhrases;
    /// with a Vault-sourced PassPhrase, the fixed salt does not degrade security.
    /// Changing this salt would invalidate all previously encrypted data — do NOT modify.
    /// </remarks>
    private static readonly byte[] KeyDerivationSalt =
    [
        0x44, 0x44, 0x46, 0x6F, 0x75, 0x6E, 0x64, 0x61,
        0x74, 0x69, 0x6F, 0x6E, 0x45, 0x6E, 0x63, 0x72
    ];

    private const int KeyDerivationIterations = 100_000;
    private const int IvSize = 16;
    private const int HmacSize = 32; // HMAC-SHA256

    private readonly byte[] _aesKey;
    private readonly byte[] _hmacKey;

    /// <inheritdoc/>
    public string ProviderName => StringEncryptionOptions.AesProviderName;

    public AesStringEncryptionProvider(
        IOptions<StringEncryptionOptions> options,
        ILogger<AesStringEncryptionProvider> logger)
    {
        StringEncryptionOptions opts = options.Value;
        string passPhrase = opts.PassPhrase;

        if (string.IsNullOrEmpty(passPhrase))
        {
            if (!opts.AllowEphemeralPassPhrase)
            {
                throw new InvalidOperationException(
                    "Encryption:PassPhrase is required. " +
                    "Configure a stable passphrase via Vault for production use, " +
                    "or set Encryption:AllowEphemeralPassPhrase to true for development.");
            }

            passPhrase = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            LogEphemeralPassPhrase(logger);
        }

        if (opts.KeySize is not (128 or 192 or 256))
        {
            throw new ArgumentException(
                $"Encryption:KeySize must be 128, 192, or 256 bits, got {opts.KeySize}.");
        }

        int aesKeySize = opts.KeySize / 8;
        byte[] derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            passPhrase,
            KeyDerivationSalt,
            KeyDerivationIterations,
            HashAlgorithmName.SHA256,
            aesKeySize + HmacSize);

        _aesKey = derivedKey[..aesKeySize];
        _hmacKey = derivedKey[aesKeySize..];
    }

    /// <inheritdoc/>
    public string Encrypt(string plainText)
    {
        byte[] iv = RandomNumberGenerator.GetBytes(IvSize);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

        using var aes = Aes.Create();
        aes.Key = _aesKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Concatenate IV, ciphertext, then append HMAC tag
        int dataLength = IvSize + cipherBytes.Length;
        byte[] output = new byte[dataLength + HmacSize];
        iv.CopyTo(output.AsSpan());
        cipherBytes.CopyTo(output.AsSpan(IvSize));

        // Encrypt-then-MAC: HMAC computed directly over the output buffer (non-overlapping regions)
        HMACSHA256.TryHashData(_hmacKey, output.AsSpan(0, dataLength), output.AsSpan(dataLength), out _);

        return Convert.ToBase64String(output);
    }

    /// <inheritdoc/>
    public string? Decrypt(string cipherText)
    {
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

        // Verify HMAC before decryption (encrypt-then-MAC: always verify first)
        int dataLength = input.Length - HmacSize;
        Span<byte> computedHmac = stackalloc byte[HmacSize];
        HMACSHA256.TryHashData(_hmacKey, input.AsSpan(0, dataLength), computedHmac, out _);

        if (!CryptographicOperations.FixedTimeEquals(computedHmac, input.AsSpan(dataLength)))
        {
            return null;
        }

        byte[] iv = input[..IvSize];
        byte[] cipherBytes = input[IvSize..dataLength];

        using var aes = Aes.Create();
        aes.Key = _aesKey;
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

    /// <inheritdoc/>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_aesKey);
        CryptographicOperations.ZeroMemory(_hmacKey);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Encryption:PassPhrase is not configured — using an ephemeral random passphrase. " +
                  "Encrypted data will NOT survive application restarts. " +
                  "Configure a stable passphrase via Vault for production use")]
    private static partial void LogEphemeralPassPhrase(ILogger logger);
}
