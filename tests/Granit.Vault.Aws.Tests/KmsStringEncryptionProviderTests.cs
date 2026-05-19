using Amazon.KeyManagementService.Model;
using Granit.Encryption;
using Granit.Encryption.Options;
using Granit.Vault.Aws.Providers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Vault.Aws.Tests;

public sealed class KmsStringEncryptionProviderTests
{
    private readonly ITransitEncryptionService _transitEncryption = Substitute.For<ITransitEncryptionService>();
    private readonly KmsStringEncryptionProvider _sut;

    public KmsStringEncryptionProviderTests()
    {
        StringEncryptionOptions encOptions = new() { VaultKeyName = "test-key" };

        _sut = new KmsStringEncryptionProvider(
            _transitEncryption,
            Microsoft.Extensions.Options.Options.Create(encOptions));
    }

    [Fact]
    public void ProviderName_ReturnsAwsKms() =>
        _sut.ProviderName.ShouldBe("AwsKms");

    [Fact]
    public void Class_Implements_IStringEncryptionProvider() =>
        _sut.ShouldBeAssignableTo<IStringEncryptionProvider>();

    [Fact]
    public void Encrypt_CallsTransitEncryptionWithKeyName()
    {
        _transitEncryption.EncryptAsync("test-key", "hello", Arg.Any<CancellationToken>())
            .Returns("encrypted-result");

        string result = _sut.Encrypt("hello");

        result.ShouldBe("encrypted-result");
    }

    [Fact]
    public void Decrypt_CallsTransitDecryptionWithKeyName()
    {
        _transitEncryption.DecryptAsync("test-key", "cipher", Arg.Any<CancellationToken>())
            .Returns("decrypted-result");

        string? result = _sut.Decrypt("cipher");

        result.ShouldBe("decrypted-result");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Decrypt_NullOrEmpty_ReturnsNull(string? input)
    {
        string? result = _sut.Decrypt(input!);

        result.ShouldBeNull();
        _transitEncryption.DidNotReceive()
            .DecryptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Decrypt_WhenExceptionThrown_ReturnsNull()
    {
        _transitEncryption.DecryptAsync("test-key", "bad-cipher", Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidCiphertextException("Invalid ciphertext"));

        string? result = _sut.Decrypt("bad-cipher");

        result.ShouldBeNull();
    }
}
