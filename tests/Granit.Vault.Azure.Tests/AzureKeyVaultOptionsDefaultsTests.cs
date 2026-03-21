using Granit.Vault.Azure.Options;
using Shouldly;
using Xunit;

namespace Granit.Vault.Azure.Tests;

public sealed class AzureKeyVaultOptionsDefaultsTests
{
    [Fact]
    public void SectionName_IsVaultAzure() => AzureKeyVaultOptions.SectionName.ShouldBe("Vault:Azure");

    [Fact]
    public void VaultUri_DefaultsToEmpty()
    {
        AzureKeyVaultOptions options = new();

        options.VaultUri.ShouldBe(string.Empty);
    }

    [Fact]
    public void EncryptionKeyName_DefaultsToEmpty()
    {
        AzureKeyVaultOptions options = new();

        options.EncryptionKeyName.ShouldBe(string.Empty);
    }

    [Fact]
    public void EncryptionAlgorithm_DefaultsToRsaOaep256()
    {
        AzureKeyVaultOptions options = new();

        options.EncryptionAlgorithm.ShouldBe("RSA-OAEP-256");
    }

    [Fact]
    public void DatabaseSecretName_DefaultsToNull()
    {
        AzureKeyVaultOptions options = new();

        options.DatabaseSecretName.ShouldBeNull();
    }

    [Fact]
    public void RotationCheckIntervalMinutes_DefaultsTo5()
    {
        AzureKeyVaultOptions options = new();

        options.RotationCheckIntervalMinutes.ShouldBe(5);
    }

    [Fact]
    public void TimeoutSeconds_DefaultsTo30()
    {
        AzureKeyVaultOptions options = new();

        options.TimeoutSeconds.ShouldBe(30);
    }
}
