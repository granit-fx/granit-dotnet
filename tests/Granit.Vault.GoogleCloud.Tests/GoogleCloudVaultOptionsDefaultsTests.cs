using Granit.Vault.GoogleCloud.Options;
using Shouldly;
using Xunit;

namespace Granit.Vault.GoogleCloud.Tests;

public sealed class GoogleCloudVaultOptionsDefaultsTests
{
    [Fact]
    public void SectionName_IsVaultGoogleCloud() => GoogleCloudVaultOptions.SectionName.ShouldBe("Vault:GoogleCloud");

    [Fact]
    public void ProjectId_DefaultsToEmpty()
    {
        GoogleCloudVaultOptions options = new();

        options.ProjectId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Location_DefaultsToGlobal()
    {
        GoogleCloudVaultOptions options = new();

        options.Location.ShouldBe("global");
    }

    [Fact]
    public void KeyRing_DefaultsToEmpty()
    {
        GoogleCloudVaultOptions options = new();

        options.KeyRing.ShouldBe(string.Empty);
    }

    [Fact]
    public void CryptoKey_DefaultsToEmpty()
    {
        GoogleCloudVaultOptions options = new();

        options.CryptoKey.ShouldBe(string.Empty);
    }

    [Fact]
    public void DatabaseSecretName_DefaultsToNull()
    {
        GoogleCloudVaultOptions options = new();

        options.DatabaseSecretName.ShouldBeNull();
    }

    [Fact]
    public void RotationCheckIntervalMinutes_DefaultsTo5()
    {
        GoogleCloudVaultOptions options = new();

        options.RotationCheckIntervalMinutes.ShouldBe(5);
    }

    [Fact]
    public void CredentialFilePath_DefaultsToNull()
    {
        GoogleCloudVaultOptions options = new();

        options.CredentialFilePath.ShouldBeNull();
    }

    [Fact]
    public void TimeoutSeconds_DefaultsTo30()
    {
        GoogleCloudVaultOptions options = new();

        options.TimeoutSeconds.ShouldBe(30);
    }
}
