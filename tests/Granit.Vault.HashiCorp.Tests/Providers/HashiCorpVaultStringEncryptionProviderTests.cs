using Granit.Encryption;
using Granit.Encryption.Options;
using Granit.Vault.HashiCorp.Providers;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VaultSharp.Core;
using Shouldly;
using Xunit;

namespace Granit.Vault.HashiCorp.Tests.Providers;

public sealed class HashiCorpVaultStringEncryptionProviderTests
{
    private const string KeyName = "test-key";

    private static HashiCorpVaultStringEncryptionProvider CreateProvider(
        ITransitEncryptionService? transitEncryption = null)
    {
        ITransitEncryptionService service = transitEncryption ?? Substitute.For<ITransitEncryptionService>();
        IOptions<StringEncryptionOptions> options = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            VaultKeyName = KeyName,
        });
        return new HashiCorpVaultStringEncryptionProvider(service, options);
    }

    [Fact]
    public void ProviderName_Returns_VaultProviderName()
    {
        HashiCorpVaultStringEncryptionProvider provider = CreateProvider();
        provider.ProviderName.ShouldBe(StringEncryptionOptions.VaultProviderName);
    }

    [Fact]
    public void Encrypt_CallsEncryptAsyncWithKeyName()
    {
        ITransitEncryptionService service = Substitute.For<ITransitEncryptionService>();
        service.EncryptAsync(KeyName, "plain", Arg.Any<CancellationToken>()).Returns("vault:v1:encrypted");
        HashiCorpVaultStringEncryptionProvider provider = CreateProvider(service);

        string result = provider.Encrypt("plain");

        result.ShouldBe("vault:v1:encrypted");
        service.Received(1).EncryptAsync(KeyName, "plain", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Decrypt_CallsDecryptAsyncWithKeyName()
    {
        ITransitEncryptionService service = Substitute.For<ITransitEncryptionService>();
        service.DecryptAsync(KeyName, "vault:v1:encrypted", Arg.Any<CancellationToken>()).Returns("plain");
        HashiCorpVaultStringEncryptionProvider provider = CreateProvider(service);

        string? result = provider.Decrypt("vault:v1:encrypted");

        result.ShouldBe("plain");
        service.Received(1).DecryptAsync(KeyName, "vault:v1:encrypted", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Decrypt_NullOrEmpty_ReturnsNull(string? cipherText)
    {
        ITransitEncryptionService service = Substitute.For<ITransitEncryptionService>();
        HashiCorpVaultStringEncryptionProvider provider = CreateProvider(service);

        string? result = provider.Decrypt(cipherText!);

        result.ShouldBeNull();
        service.DidNotReceive().DecryptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Decrypt_VaultBadRequest_ReturnsNull()
    {
        ITransitEncryptionService service = Substitute.For<ITransitEncryptionService>();
        service.DecryptAsync(KeyName, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new VaultApiException(System.Net.HttpStatusCode.BadRequest, "invalid ciphertext"));
        HashiCorpVaultStringEncryptionProvider provider = CreateProvider(service);

        string? result = provider.Decrypt("vault:v1:corrupted");

        result.ShouldBeNull("VaultApiException with 400 must return null (invalid ciphertext)");
    }

    [Fact]
    public void Decrypt_InfrastructureError_Propagates()
    {
        ITransitEncryptionService service = Substitute.For<ITransitEncryptionService>();
        service.DecryptAsync(KeyName, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Vault unavailable"));
        HashiCorpVaultStringEncryptionProvider provider = CreateProvider(service);

        Should.Throw<InvalidOperationException>(() => provider.Decrypt("vault:v1:corrupted"));
    }
}
