// =============================================================================
// Tests - AesCacheValueEncryptor
// =============================================================================
// Vérifie que le chiffrement/déchiffrement AES-256-GCM fonctionne correctement :
//   - Encrypt(Decrypt(x)) == x (round-trip)
//   - Nonce aléatoire : deux chiffrements du même plaintext donnent des ciphertexts différents
//   - Rejet d'une clé invalide (taille != 32 octets)
//   - Détection de falsification via le tag d'authentification GCM
// =============================================================================

using System.Security.Cryptography;
using Granit.Caching.Options;
using Shouldly;
using Xunit;

namespace Granit.Caching.Tests;

public sealed class AesCacheValueEncryptorTests
{
    private static AesCacheValueEncryptor CreateEncryptor(string? keyBase64 = null)
    {
        if (keyBase64 is null)
        {
            byte[] key = new byte[32];
            RandomNumberGenerator.Fill(key);
            keyBase64 = Convert.ToBase64String(key);
        }

        CacheEncryptionOptions encryptionOptions = new() { Key = keyBase64 };
        return new AesCacheValueEncryptor(Microsoft.Extensions.Options.Options.Create(encryptionOptions));
    }

    [Fact]
    public void EncryptThenDecrypt_ReturnsOriginalPlaintext()
    {
        // Arrange
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = "données de santé sensibles"u8.ToArray();

        // Act
        byte[] ciphertext = encryptor.Encrypt(plaintext);
        byte[] decrypted = encryptor.Decrypt(ciphertext);

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void Encrypt_SamePlaintext_ProducesDifferentCiphertexts()
    {
        // Arrange — nonce aléatoire par opération
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = "test stampede ISO27001"u8.ToArray();

        // Act
        byte[] cipher1 = encryptor.Encrypt(plaintext);
        byte[] cipher2 = encryptor.Encrypt(plaintext);

        // Assert
        cipher1.ShouldNotBe(cipher2, "le nonce aléatoire doit produire des ciphertexts distincts");
    }

    [Fact]
    public void Encrypt_OutputFormat_ContainsNonceAndTag()
    {
        // Arrange — format : [12 octets Nonce][16 octets Tag][N octets CipherText]
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = "vérification format nonce+tag"u8.ToArray();

        // Act
        byte[] ciphertext = encryptor.Encrypt(plaintext);

        // Assert — le ciphertext doit être plus long que l'overhead (28 octets)
        ciphertext.Length.ShouldBeGreaterThan(28);
    }

    [Fact]
    public void Constructor_KeyNot32Bytes_ThrowsArgumentException()
    {
        // Arrange — clé AES invalide (16 octets au lieu de 32)
        byte[] shortKey = new byte[16];
        RandomNumberGenerator.Fill(shortKey);
        CacheEncryptionOptions opts = new() { Key = Convert.ToBase64String(shortKey) };

        // Act
        Action act = () => _ = new AesCacheValueEncryptor(Microsoft.Extensions.Options.Options.Create(opts));

        // Assert
        Should.Throw<ArgumentException>(act).Message.ShouldContain("256");
    }

    [Fact]
    public void Constructor_NullKey_ThrowsInvalidOperationException()
    {
        // Arrange
        CacheEncryptionOptions opts = new() { Key = null };

        // Act
        Action act = () => _ = new AesCacheValueEncryptor(Microsoft.Extensions.Options.Options.Create(opts));

        // Assert
        Should.Throw<InvalidOperationException>(act);
    }

    [Fact]
    public void EncryptThenDecrypt_EmptyByteArray_ReturnsEmpty()
    {
        // Arrange
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = [];

        // Act
        byte[] ciphertext = encryptor.Encrypt(plaintext);
        byte[] decrypted = encryptor.Decrypt(ciphertext);

        // Assert
        decrypted.ShouldBeEmpty();
    }

    [Fact]
    public void Decrypt_CiphertextShorterThanOverhead_ThrowsArgumentException()
    {
        // Arrange — ciphertext invalide : moins de 28 octets (nonce 12 + tag 16)
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] tooShort = new byte[20];

        // Act
        Action act = () => encryptor.Decrypt(tooShort);

        // Assert
        Should.Throw<ArgumentException>(act).Message.ShouldContain("28");
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        // Arrange — GCM détecte la falsification via le tag d'authentification
        AesCacheValueEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = "données intègres"u8.ToArray();
        byte[] ciphertext = encryptor.Encrypt(plaintext);

        // Tamper with the ciphertext body (after nonce+tag, byte index 28+)
        if (ciphertext.Length > 28)
        {
            ciphertext[28] ^= 0xFF;
        }

        // Act & Assert
        Should.Throw<CryptographicException>(() => encryptor.Decrypt(ciphertext));
    }
}
