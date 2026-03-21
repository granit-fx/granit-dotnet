// =============================================================================
// DefaultStringEncryptionServiceTests - Tests du service principal
// =============================================================================

using Granit.Encryption;
using Granit.Encryption.Options;
using Granit.Encryption.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Encryption.Tests;

public sealed class DefaultStringEncryptionServiceTests
{
    private static IOptions<StringEncryptionOptions> OptionsFor(string providerName) =>
        Microsoft.Extensions.Options.Options.Create(new StringEncryptionOptions { ProviderName = providerName });

    [Fact]
    public void Encrypt_Delegates_To_Selected_Provider()
    {
        IStringEncryptionProvider provider = Substitute.For<IStringEncryptionProvider>();
        provider.ProviderName.Returns("Aes");
        provider.Encrypt("hello").Returns("chiffré");

        DefaultStringEncryptionService service = new(
            [provider],
            OptionsFor("Aes"));

        string result = service.Encrypt("hello");

        result.ShouldBe("chiffré");
        provider.Received(1).Encrypt("hello");
    }

    [Fact]
    public void Decrypt_Delegates_To_Selected_Provider()
    {
        IStringEncryptionProvider provider = Substitute.For<IStringEncryptionProvider>();
        provider.ProviderName.Returns("Aes");
        provider.Decrypt("chiffré").Returns("hello");

        DefaultStringEncryptionService service = new(
            [provider],
            OptionsFor("Aes"));

        string? result = service.Decrypt("chiffré");

        result.ShouldBe("hello");
        provider.Received(1).Decrypt("chiffré");
    }

    [Fact]
    public void Constructor_UnknownProvider_Throws_InvalidOperationException()
    {
        IStringEncryptionProvider aesProvider = Substitute.For<IStringEncryptionProvider>();
        aesProvider.ProviderName.Returns("Aes");

        Action act = () => _ = new DefaultStringEncryptionService(
            [aesProvider],
            OptionsFor("Vault"));

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("Vault");
    }

    [Fact]
    public void Constructor_MultipleProviders_Selects_Correct_One()
    {
        IStringEncryptionProvider aesProvider = Substitute.For<IStringEncryptionProvider>();
        aesProvider.ProviderName.Returns("Aes");

        IStringEncryptionProvider vaultProvider = Substitute.For<IStringEncryptionProvider>();
        vaultProvider.ProviderName.Returns("Vault");
        vaultProvider.Encrypt("secret").Returns("vault-chiffré");

        DefaultStringEncryptionService service = new(
            [aesProvider, vaultProvider],
            OptionsFor("Vault"));

        service.Encrypt("secret");

        vaultProvider.Received(1).Encrypt("secret");
        aesProvider.DidNotReceive().Encrypt(Arg.Any<string>());
    }

    [Fact]
    public void Constructor_NoProviders_Throws_InvalidOperationException()
    {
        Action act = () => _ = new DefaultStringEncryptionService(
            [],
            OptionsFor("Aes"));

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("Aes");
    }

    [Fact]
    public void Constructor_ErrorMessage_ListsAvailableProviders()
    {
        IStringEncryptionProvider aesProvider = Substitute.For<IStringEncryptionProvider>();
        aesProvider.ProviderName.Returns("Aes");

        IStringEncryptionProvider customProvider = Substitute.For<IStringEncryptionProvider>();
        customProvider.ProviderName.Returns("Custom");

        Action act = () => _ = new DefaultStringEncryptionService(
            [aesProvider, customProvider],
            OptionsFor("Vault"));

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(act);
        ex.Message.ShouldContain("Aes");
        ex.Message.ShouldContain("Custom");
        ex.Message.ShouldContain("Vault");
    }

    [Fact]
    public void Decrypt_ReturnsNull_WhenProviderReturnsNull()
    {
        IStringEncryptionProvider provider = Substitute.For<IStringEncryptionProvider>();
        provider.ProviderName.Returns("Aes");
        provider.Decrypt("bad-cipher").Returns((string?)null);

        DefaultStringEncryptionService service = new(
            [provider],
            OptionsFor("Aes"));

        string? result = service.Decrypt("bad-cipher");

        result.ShouldBeNull();
        provider.Received(1).Decrypt("bad-cipher");
    }
}
