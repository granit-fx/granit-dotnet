using Granit.Vault.Aws.Options;
using Shouldly;
using Xunit;

namespace Granit.Vault.Aws.Tests;

public sealed class AwsVaultOptionsDefaultsTests
{
    [Fact]
    public void SectionName_IsVaultAws() => AwsVaultOptions.SectionName.ShouldBe("Vault:Aws");

    [Fact]
    public void Region_DefaultsToEmpty()
    {
        AwsVaultOptions options = new();

        options.Region.ShouldBe(string.Empty);
    }

    [Fact]
    public void KmsKeyId_DefaultsToEmpty()
    {
        AwsVaultOptions options = new();

        options.KmsKeyId.ShouldBe(string.Empty);
    }

    [Fact]
    public void DatabaseSecretArn_DefaultsToNull()
    {
        AwsVaultOptions options = new();

        options.DatabaseSecretArn.ShouldBeNull();
    }

    [Fact]
    public void RotationCheckIntervalMinutes_DefaultsTo5()
    {
        AwsVaultOptions options = new();

        options.RotationCheckIntervalMinutes.ShouldBe(5);
    }

    [Fact]
    public void AccessKeyId_DefaultsToNull()
    {
        AwsVaultOptions options = new();

        options.AccessKeyId.ShouldBeNull();
    }

    [Fact]
    public void SecretAccessKey_DefaultsToNull()
    {
        AwsVaultOptions options = new();

        options.SecretAccessKey.ShouldBeNull();
    }

    [Fact]
    public void TimeoutSeconds_DefaultsTo30()
    {
        AwsVaultOptions options = new();

        options.TimeoutSeconds.ShouldBe(30);
    }
}
