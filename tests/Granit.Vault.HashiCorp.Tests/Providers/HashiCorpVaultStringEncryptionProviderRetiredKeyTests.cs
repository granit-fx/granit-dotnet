using Granit.Encryption.Options;
using Granit.Vault.Exceptions;
using Granit.Vault.HashiCorp.Providers;
using Granit.Vault.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Vault.HashiCorp.Tests.Providers;

public sealed class HashiCorpVaultStringEncryptionProviderRetiredKeyTests
{
    private const string KeyName = "test-key";

    [Fact]
    public void Decrypt_RetiredKeyVersion_ThrowsRetiredKeyVersionException()
    {
        ITransitEncryptionService service = Substitute.For<ITransitEncryptionService>();
        service.GetKeyVersion("vault:v1:data").Returns("v1");

        IOptions<StringEncryptionOptions> encOptions = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            VaultKeyName = KeyName,
        });
        IOptions<ReEncryptionOptions> reEncOptions = Microsoft.Extensions.Options.Options.Create(new ReEncryptionOptions());
        reEncOptions.Value.RetiredKeyVersions.Add("v1");

        HashiCorpVaultStringEncryptionProvider provider = new(service, encOptions, reEncOptions);

        Should.Throw<RetiredKeyVersionException>(() => provider.Decrypt("vault:v1:data"))
            .KeyVersion.ShouldBe("v1");
    }

    [Fact]
    public void Decrypt_NonRetiredKeyVersion_DecryptsSuccessfully()
    {
        ITransitEncryptionService service = Substitute.For<ITransitEncryptionService>();
        service.GetKeyVersion("vault:v2:data").Returns("v2");
        service.DecryptAsync(KeyName, "vault:v2:data", Arg.Any<CancellationToken>()).Returns("decrypted");

        IOptions<StringEncryptionOptions> encOptions = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            VaultKeyName = KeyName,
        });
        IOptions<ReEncryptionOptions> reEncOptions = Microsoft.Extensions.Options.Options.Create(new ReEncryptionOptions());
        reEncOptions.Value.RetiredKeyVersions.Add("v1");

        HashiCorpVaultStringEncryptionProvider provider = new(service, encOptions, reEncOptions);

        string? result = provider.Decrypt("vault:v2:data");

        result.ShouldBe("decrypted");
    }

    [Fact]
    public void Decrypt_NullKeyVersion_DoesNotThrowRetiredKeyException()
    {
        ITransitEncryptionService service = Substitute.For<ITransitEncryptionService>();
        service.GetKeyVersion("base64data").Returns((string?)null);
        service.DecryptAsync(KeyName, "base64data", Arg.Any<CancellationToken>()).Returns("decrypted");

        IOptions<StringEncryptionOptions> encOptions = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            VaultKeyName = KeyName,
        });
        IOptions<ReEncryptionOptions> reEncOptions = Microsoft.Extensions.Options.Options.Create(new ReEncryptionOptions());
        reEncOptions.Value.RetiredKeyVersions.Add("v1");

        HashiCorpVaultStringEncryptionProvider provider = new(service, encOptions, reEncOptions);

        string? result = provider.Decrypt("base64data");

        result.ShouldBe("decrypted");
    }

    [Fact]
    public void Constructor_WithoutReEncryptionOptions_DoesNotThrow()
    {
        ITransitEncryptionService service = Substitute.For<ITransitEncryptionService>();
        service.GetKeyVersion("vault:v1:data").Returns("v1");
        service.DecryptAsync(KeyName, "vault:v1:data", Arg.Any<CancellationToken>()).Returns("decrypted");

        IOptions<StringEncryptionOptions> encOptions = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            VaultKeyName = KeyName,
        });

        // No re-encryption options — retired versions should be empty
        HashiCorpVaultStringEncryptionProvider provider = new(service, encOptions);

        string? result = provider.Decrypt("vault:v1:data");

        result.ShouldBe("decrypted");
    }

    [Fact]
    public void Decrypt_RetiredKeyVersionCheck_IsCaseInsensitive()
    {
        ITransitEncryptionService service = Substitute.For<ITransitEncryptionService>();
        service.GetKeyVersion("vault:v1:data").Returns("V1");

        IOptions<StringEncryptionOptions> encOptions = Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions
        {
            VaultKeyName = KeyName,
        });
        IOptions<ReEncryptionOptions> reEncOptions = Microsoft.Extensions.Options.Options.Create(new ReEncryptionOptions());
        reEncOptions.Value.RetiredKeyVersions.Add("v1");

        HashiCorpVaultStringEncryptionProvider provider = new(service, encOptions, reEncOptions);

        Should.Throw<RetiredKeyVersionException>(() => provider.Decrypt("vault:v1:data"));
    }
}
