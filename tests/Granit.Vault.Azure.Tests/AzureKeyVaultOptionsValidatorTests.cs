using Granit.Vault.Azure.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Vault.Azure.Tests;

public sealed class AzureKeyVaultOptionsValidatorTests
{
    private readonly AzureKeyVaultOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_ReturnsSuccess()
    {
        AzureKeyVaultOptions options = new()
        {
            VaultUri = "https://my-vault.vault.azure.net/",
            EncryptionKeyName = "granit-encryption",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyVaultUri_ReturnsFail(string? vaultUri)
    {
        AzureKeyVaultOptions options = new()
        {
            VaultUri = vaultUri!,
            EncryptionKeyName = "key",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("VaultUri");
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("ftp://vault.azure.net")]
    public void Validate_InvalidVaultUri_ReturnsFail(string vaultUri)
    {
        AzureKeyVaultOptions options = new()
        {
            VaultUri = vaultUri,
            EncryptionKeyName = "key",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("VaultUri");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyEncryptionKeyName_ReturnsFail(string? keyName)
    {
        AzureKeyVaultOptions options = new()
        {
            VaultUri = "https://my-vault.vault.azure.net/",
            EncryptionKeyName = keyName!,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("EncryptionKeyName");
    }

    [Theory]
    [InlineData("RSA-OAEP")]
    [InlineData("RSA-OAEP-256")]
    public void Validate_SupportedAlgorithm_ReturnsSuccess(string algorithm)
    {
        AzureKeyVaultOptions options = new()
        {
            VaultUri = "https://my-vault.vault.azure.net/",
            EncryptionKeyName = "key",
            EncryptionAlgorithm = algorithm,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("AES-256")]
    [InlineData("RSA1_5")]
    public void Validate_UnsupportedAlgorithm_ReturnsFail(string algorithm)
    {
        AzureKeyVaultOptions options = new()
        {
            VaultUri = "https://my-vault.vault.azure.net/",
            EncryptionKeyName = "key",
            EncryptionAlgorithm = algorithm,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("EncryptionAlgorithm");
    }

    [Fact]
    public void Validate_RotationIntervalLessThanOne_ReturnsFail()
    {
        AzureKeyVaultOptions options = new()
        {
            VaultUri = "https://my-vault.vault.azure.net/",
            EncryptionKeyName = "key",
            RotationCheckIntervalMinutes = 0,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("RotationCheckIntervalMinutes");
    }

    [Fact]
    public void Validate_TimeoutLessThanOne_ReturnsFail()
    {
        AzureKeyVaultOptions options = new()
        {
            VaultUri = "https://my-vault.vault.azure.net/",
            EncryptionKeyName = "key",
            TimeoutSeconds = 0,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TimeoutSeconds");
    }

    [Fact]
    public void Validate_HttpUri_ReturnsFail()
    {
        AzureKeyVaultOptions options = new()
        {
            VaultUri = "http://localhost:8080/",
            EncryptionKeyName = "key",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("HTTPS");
    }
}
