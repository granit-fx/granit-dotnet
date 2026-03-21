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
    public void Encrypt_OutputLength_IsIvPlusCiphertext()
    {
        // Arrange — AES-CBC with PKCS7 padding adds up to 16 bytes
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = "test data"u8.ToArray();

        // Act
        byte[] ciphertext = encryptor.Encrypt(plaintext);

        // Assert — [16 bytes IV] + [at least 1 block of ciphertext]
        ciphertext.Length.ShouldBeGreaterThanOrEqualTo(16 + 16);
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
    public void Decrypt_ExactlyIvSize_EmptyCiphertextBody_ReturnsEmpty()
    {
        // Arrange — 16 bytes is exactly IV size, empty ciphertext body
        // AES-CBC with PKCS7 padding: TransformFinalBlock with 0 bytes returns empty
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] exactIvSize = new byte[16];

        // Act — should not throw ArgumentException (ciphertext.Length >= IvSizeBytes passes)
        byte[] result = encryptor.Decrypt(exactIvSize);

        // Assert
        result.ShouldBeEmpty();
    }
}
