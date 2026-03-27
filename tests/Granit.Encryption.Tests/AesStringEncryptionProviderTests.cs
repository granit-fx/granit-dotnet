// =============================================================================
// AesStringEncryptionProviderTests - Tests unitaires AES-256-CBC
// =============================================================================

using Granit.Encryption;
using Granit.Encryption.Options;
using Granit.Encryption.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Encryption.Tests;

public sealed class AesStringEncryptionProviderTests
{
    private static AesStringEncryptionProvider CreateProvider(string passPhrase = "P@ssw0rdVaultSecret!ISO270012026")
    {
        IOptions<StringEncryptionOptions> options = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            PassPhrase = passPhrase,
            KeySize = 256,
            ProviderName = StringEncryptionOptions.AesProviderName
        });

        return new AesStringEncryptionProvider(options, NullLogger<AesStringEncryptionProvider>.Instance);
    }

    [Fact]
    public void ProviderName_Returns_Aes()
    {
        AesStringEncryptionProvider provider = CreateProvider();

        provider.ProviderName.ShouldBe("Aes");
    }

    [Theory]
    [InlineData("Bonjour monde !")]
    [InlineData("données de santé sensibles")]
    [InlineData("")]
    [InlineData("caractères Unicode : éàü €™©")]
    public void Encrypt_Decrypt_RoundTrip_Returns_Original(string plainText)
    {
        AesStringEncryptionProvider provider = CreateProvider();

        string cipherText = provider.Encrypt(plainText);
        string? decrypted = provider.Decrypt(cipherText);

        decrypted.ShouldBe(plainText);
    }

    [Fact]
    public void Encrypt_SameInput_Produces_DifferentCipherTexts()
    {
        // CWE-329 : chaque chiffrement doit produire un IV différent.
        AesStringEncryptionProvider provider = CreateProvider();
        string plainText = "texte identique";

        string cipher1 = provider.Encrypt(plainText);
        string cipher2 = provider.Encrypt(plainText);

        cipher1.ShouldNotBe(cipher2, "l'IV aléatoire doit produire des ciphertexts distincts");
    }

    [Fact]
    public void Encrypt_Output_Is_ValidBase64()
    {
        AesStringEncryptionProvider provider = CreateProvider();

        string cipherText = provider.Encrypt("test");

        byte[] bytes = Convert.FromBase64String(cipherText);
        // IV(16) + at least one AES block(16) + HMAC-SHA256(32) = 64 bytes minimum
        bytes.Length.ShouldBeGreaterThanOrEqualTo(64, "le ciphertext doit contenir IV(16) + bloc AES + HMAC(32)");
    }

    [Fact]
    public void Decrypt_Null_Returns_Null()
    {
        AesStringEncryptionProvider provider = CreateProvider();

        string? result = provider.Decrypt(null!);

        result.ShouldBeNull();
    }

    [Fact]
    public void Decrypt_Empty_Returns_Null()
    {
        AesStringEncryptionProvider provider = CreateProvider();

        string? result = provider.Decrypt(string.Empty);

        result.ShouldBeNull();
    }

    [Fact]
    public void Decrypt_InvalidBase64_Returns_Null()
    {
        AesStringEncryptionProvider provider = CreateProvider();

        string? result = provider.Decrypt("ceci-n-est-pas-du-base64!!!");

        result.ShouldBeNull();
    }

    [Fact]
    public void Decrypt_TamperedCipherText_Returns_Null()
    {
        AesStringEncryptionProvider provider = CreateProvider();
        string cipherText = provider.Encrypt("données sensibles");

        // Altérer le ciphertext pour simuler une falsification
        byte[] bytes = Convert.FromBase64String(cipherText);
        bytes[^1] ^= 0xFF;
        string tampered = Convert.ToBase64String(bytes);

        string? result = provider.Decrypt(tampered);

        result.ShouldBeNull("un ciphertext falsifié doit échouer silencieusement");
    }

    [Fact]
    public void Decrypt_TooShortInput_Returns_Null()
    {
        AesStringEncryptionProvider provider = CreateProvider();
        // Moins de 64 octets (IV=16 + bloc AES=16 + HMAC-SHA256=32)
        string tooShort = Convert.ToBase64String(new byte[50]);

        string? result = provider.Decrypt(tooShort);

        result.ShouldBeNull();
    }

    [Fact]
    public void Constructor_EmptyPassPhrase_Uses_Ephemeral_Key_And_Still_Works()
    {
        IOptions<StringEncryptionOptions> options = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            PassPhrase = string.Empty,
            AllowEphemeralPassPhrase = true
        });

        AesStringEncryptionProvider provider = new(options, NullLogger<AesStringEncryptionProvider>.Instance);

        string cipherText = provider.Encrypt("ephemeral empty test");
        provider.Decrypt(cipherText).ShouldBe("ephemeral empty test");
    }

    [Fact]
    public void Decrypt_WrongKey_Returns_Null()
    {
        AesStringEncryptionProvider providerA = CreateProvider("clé-A-vault-secret");
        AesStringEncryptionProvider providerB = CreateProvider("clé-B-vault-secret");

        string cipherText = providerA.Encrypt("données confidentielles");
        string? result = providerB.Decrypt(cipherText);

        result.ShouldBeNull("un ciphertext chiffré avec la clé A ne peut pas être déchiffré avec la clé B");
    }

    [Fact]
    public void Constructor_NullPassPhrase_Uses_Ephemeral_Key_And_Still_Works()
    {
        IOptions<StringEncryptionOptions> options = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            PassPhrase = null!,
            AllowEphemeralPassPhrase = true
        });

        AesStringEncryptionProvider provider = new(options, NullLogger<AesStringEncryptionProvider>.Instance);

        string cipherText = provider.Encrypt("ephemeral test");
        provider.Decrypt(cipherText).ShouldBe("ephemeral test");
    }

    [Fact]
    public void Encrypt_Decrypt_RoundTrip_LongString_Succeeds()
    {
        AesStringEncryptionProvider provider = CreateProvider();
        string longText = new('A', 10_000);

        string cipherText = provider.Encrypt(longText);
        string? decrypted = provider.Decrypt(cipherText);

        decrypted.ShouldBe(longText);
    }

    [Fact]
    public void Decrypt_ValidBase64_ButTamperedHmac_Returns_Null()
    {
        AesStringEncryptionProvider provider = CreateProvider();
        string cipherText = provider.Encrypt("test value");

        // Tamper with the HMAC portion (last 32 bytes) — flip a byte in the middle
        byte[] bytes = Convert.FromBase64String(cipherText);
        bytes[^16] ^= 0xFF;
        string tampered = Convert.ToBase64String(bytes);

        string? result = provider.Decrypt(tampered);

        result.ShouldBeNull();
    }

    [Fact]
    public void Decrypt_ValidBase64_ButTamperedIv_Returns_Null()
    {
        AesStringEncryptionProvider provider = CreateProvider();
        string cipherText = provider.Encrypt("test value");

        // Tamper with the IV portion (first 16 bytes)
        byte[] bytes = Convert.FromBase64String(cipherText);
        bytes[0] ^= 0xFF;
        string tampered = Convert.ToBase64String(bytes);

        string? result = provider.Decrypt(tampered);

        // HMAC verification will fail because IV is part of the MAC input
        result.ShouldBeNull();
    }

    [Fact]
    public void Decrypt_ExactlyMinimumLength_ButInvalid_Returns_Null()
    {
        AesStringEncryptionProvider provider = CreateProvider();
        // Exactly 64 bytes: IV(16) + one AES block(16) + HMAC(32) — but random data
        string exactMinimum = Convert.ToBase64String(new byte[64]);

        string? result = provider.Decrypt(exactMinimum);

        result.ShouldBeNull();
    }

    [Fact]
    public void Encrypt_Decrypt_RoundTrip_128BitKey_Succeeds()
    {
        IOptions<StringEncryptionOptions> options = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            PassPhrase = "TestPassPhrase128BitKey!",
            KeySize = 128,
            ProviderName = StringEncryptionOptions.AesProviderName
        });
        AesStringEncryptionProvider provider = new(options, NullLogger<AesStringEncryptionProvider>.Instance);

        string cipherText = provider.Encrypt("hello 128-bit");
        string? decrypted = provider.Decrypt(cipherText);

        decrypted.ShouldBe("hello 128-bit");
    }

    [Fact]
    public void Encrypt_Decrypt_RoundTrip_192BitKey_Succeeds()
    {
        IOptions<StringEncryptionOptions> options = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            PassPhrase = "TestPassPhrase192BitKey!",
            KeySize = 192,
            ProviderName = StringEncryptionOptions.AesProviderName
        });
        AesStringEncryptionProvider provider = new(options, NullLogger<AesStringEncryptionProvider>.Instance);

        string cipherText = provider.Encrypt("hello 192-bit");
        string? decrypted = provider.Decrypt(cipherText);

        decrypted.ShouldBe("hello 192-bit");
    }

    [Fact]
    public void Decrypt_DifferentKeySize_Returns_Null()
    {
        IOptions<StringEncryptionOptions> options256 = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            PassPhrase = "SamePassPhraseForBothKeys!",
            KeySize = 256
        });
        IOptions<StringEncryptionOptions> options128 = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            PassPhrase = "SamePassPhraseForBothKeys!",
            KeySize = 128
        });

        AesStringEncryptionProvider provider256 = new(options256, NullLogger<AesStringEncryptionProvider>.Instance);
        AesStringEncryptionProvider provider128 = new(options128, NullLogger<AesStringEncryptionProvider>.Instance);

        string cipherText = provider256.Encrypt("cross-key test");
        string? result = provider128.Decrypt(cipherText);

        result.ShouldBeNull();
    }
}
