using System.Security.Cryptography;
using Granit.Encryption.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.Encryption.EntityFrameworkCore.Tests;

#pragma warning disable EF1001 // IsolatedFieldEncryptor is Granit-internal, not EF Core-internal
public sealed class IsolatedFieldEncryptorTests
{
    private static byte[] GenerateKey() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Encrypt_Decrypt_RoundTrip_ReturnsOriginalPlaintext()
    {
        byte[] key = GenerateKey();
        const string plainText = "SSN-123-45-6789";

        string cipherText = IsolatedFieldEncryptor.Encrypt(key, plainText);
        string? decrypted = IsolatedFieldEncryptor.Decrypt(key, cipherText);

        decrypted.ShouldBe(plainText);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertext_ForSamePlaintext()
    {
        byte[] key = GenerateKey();
        const string plainText = "same-value";

        string cipher1 = IsolatedFieldEncryptor.Encrypt(key, plainText);
        string cipher2 = IsolatedFieldEncryptor.Encrypt(key, plainText);

        cipher1.ShouldNotBe(cipher2, "Random IV should produce different ciphertext each time");
    }

    [Fact]
    public void Decrypt_WithWrongKey_ReturnsNull()
    {
        byte[] key1 = GenerateKey();
        byte[] key2 = GenerateKey();
        const string plainText = "secret-data";

        string cipherText = IsolatedFieldEncryptor.Encrypt(key1, plainText);
        string? result = IsolatedFieldEncryptor.Decrypt(key2, cipherText);

        result.ShouldBeNull();
    }

    [Fact]
    public void Decrypt_WithTamperedCiphertext_ReturnsNull()
    {
        byte[] key = GenerateKey();
        string cipherText = IsolatedFieldEncryptor.Encrypt(key, "sensitive-data");

        // Tamper with the ciphertext
        byte[] raw = Convert.FromBase64String(cipherText);
        raw[20] ^= 0xFF;
        string tampered = Convert.ToBase64String(raw);

        string? result = IsolatedFieldEncryptor.Decrypt(key, tampered);

        result.ShouldBeNull("HMAC verification should fail on tampered ciphertext");
    }

    [Fact]
    public void Decrypt_WithEmptyString_ReturnsNull()
    {
        byte[] key = GenerateKey();

        IsolatedFieldEncryptor.Decrypt(key, string.Empty).ShouldBeNull();
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ReturnsNull()
    {
        byte[] key = GenerateKey();

        IsolatedFieldEncryptor.Decrypt(key, "not-base64!!!").ShouldBeNull();
    }

    [Fact]
    public void Decrypt_WithTooShortInput_ReturnsNull()
    {
        byte[] key = GenerateKey();

        IsolatedFieldEncryptor.Decrypt(key, Convert.ToBase64String(new byte[10])).ShouldBeNull();
    }

    [Fact]
    public void Encrypt_WithInvalidKeySize_ThrowsArgumentException()
    {
        byte[] shortKey = new byte[16]; // AES-128, not AES-256

        Should.Throw<ArgumentException>(() =>
            IsolatedFieldEncryptor.Encrypt(shortKey, "test"));
    }

    [Fact]
    public void Decrypt_WithInvalidKeySize_ThrowsArgumentException()
    {
        byte[] shortKey = new byte[16];

        Should.Throw<ArgumentException>(() =>
            IsolatedFieldEncryptor.Decrypt(shortKey, "dGVzdA=="));
    }

    [Fact]
    public void Encrypt_Decrypt_HandlesUnicodeContent()
    {
        byte[] key = GenerateKey();
        const string plainText = "Données personnelles: médécin traitant — GDPR Art. 17 🔐";

        string cipherText = IsolatedFieldEncryptor.Encrypt(key, plainText);
        string? decrypted = IsolatedFieldEncryptor.Decrypt(key, cipherText);

        decrypted.ShouldBe(plainText);
    }

    [Fact]
    public void Encrypt_Decrypt_HandlesLargeContent()
    {
        byte[] key = GenerateKey();
        string plainText = new('A', 100_000);

        string cipherText = IsolatedFieldEncryptor.Encrypt(key, plainText);
        string? decrypted = IsolatedFieldEncryptor.Decrypt(key, cipherText);

        decrypted.ShouldBe(plainText);
    }
}
