using System.Security.Cryptography;
using Granit.Caching.Options;
using Shouldly;
using Xunit;

namespace Granit.Caching.Tests;

public sealed class AesCacheValueEncryptorAdditionalTests
{
    private static AesCacheValueEncryptor CreateEncryptor()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);
        CacheEncryptionOptions options = new() { Key = Convert.ToBase64String(key) };
        return new AesCacheValueEncryptor(Microsoft.Extensions.Options.Options.Create(options));
    }

    [Fact]
    public void Constructor_WhitespaceKey_ThrowsInvalidOperationException()
    {
        // Arrange
        CacheEncryptionOptions opts = new() { Key = "   " };

        // Act
        Action act = () => _ = new AesCacheValueEncryptor(Microsoft.Extensions.Options.Options.Create(opts));

        // Assert
        Should.Throw<InvalidOperationException>(act);
    }

    [Fact]
    public void Constructor_EmptyStringKey_ThrowsInvalidOperationException()
    {
        // Arrange
        CacheEncryptionOptions opts = new() { Key = "" };

        // Act
        Action act = () => _ = new AesCacheValueEncryptor(Microsoft.Extensions.Options.Options.Create(opts));

        // Assert
        Should.Throw<InvalidOperationException>(act);
    }

    [Fact]
    public void EncryptThenDecrypt_LargePayload_ReturnsOriginal()
    {
        // Arrange — 64 KB payload simulating large cached JSON
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = new byte[65536];
        RandomNumberGenerator.Fill(plaintext);

        // Act
        byte[] ciphertext = encryptor.Encrypt(plaintext);
        byte[] decrypted = encryptor.Decrypt(ciphertext);

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void Encrypt_OutputLength_IsNoncePlusTagPlusCiphertext()
    {
        // Arrange — AES-GCM : pas de padding, ciphertext.Length == plaintext.Length
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = "test data"u8.ToArray();

        // Act
        byte[] ciphertext = encryptor.Encrypt(plaintext);

        // Assert — [12 bytes Nonce] + [16 bytes Tag] + [plaintext.Length bytes CipherText]
        ciphertext.Length.ShouldBe(12 + 16 + plaintext.Length);
    }

    [Fact]
    public void Decrypt_DifferentKeyInstance_ThrowsCryptographicException()
    {
        // Arrange — encrypt with one key, decrypt with another
        AesCacheValueEncryptor encryptor1 = CreateEncryptor();
        AesCacheValueEncryptor encryptor2 = CreateEncryptor();
        byte[] plaintext = "cross-key test"u8.ToArray();

        byte[] ciphertext = encryptor1.Encrypt(plaintext);

        // Act & Assert — wrong key should cause a CryptographicException
        Should.Throw<CryptographicException>(() => encryptor2.Decrypt(ciphertext));
    }

    [Fact]
    public void Decrypt_TamperedTag_ThrowsCryptographicException()
    {
        // Arrange — flip a bit in the authentication tag (bytes 12-27)
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = "tag integrity check"u8.ToArray();
        byte[] ciphertext = encryptor.Encrypt(plaintext);

        ciphertext[15] ^= 0x01; // tamper with tag byte

        // Act & Assert — GCM tag validation must detect tampering
        Should.Throw<CryptographicException>(() => encryptor.Decrypt(ciphertext));
    }

    [Fact]
    public void Decrypt_ExactlyOverhead_EmptyCiphertextBody_ReturnsEmpty()
    {
        // Arrange — encrypt an empty plaintext, then decrypt
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] ciphertext = encryptor.Encrypt([]);

        // Assert — overhead only: 12 (nonce) + 16 (tag) + 0 (ciphertext)
        ciphertext.Length.ShouldBe(28);

        // Act
        byte[] result = encryptor.Decrypt(ciphertext);

        // Assert
        result.ShouldBeEmpty();
    }
}
