using Granit.Vault.Options;
using Shouldly;
using Xunit;

namespace Granit.Vault.Tests;

public sealed class ReEncryptionOptionsTests
{
    [Fact]
    public void SectionName_IsReEncryption() => ReEncryptionOptions.SectionName.ShouldBe("ReEncryption");

    [Fact]
    public void RetiredKeyVersions_DefaultsToEmptySet()
    {
        ReEncryptionOptions options = new();

        options.RetiredKeyVersions.ShouldNotBeNull();
        options.RetiredKeyVersions.ShouldBeEmpty();
    }

    [Fact]
    public void BatchSize_DefaultsTo500()
    {
        ReEncryptionOptions options = new();

        options.BatchSize.ShouldBe(500);
    }

    [Fact]
    public void RetiredKeyVersions_IsCaseInsensitive()
    {
        ReEncryptionOptions options = new();
        options.RetiredKeyVersions.Add("V1");

        options.RetiredKeyVersions.Contains("v1").ShouldBeTrue();
        options.RetiredKeyVersions.Contains("V1").ShouldBeTrue();
    }

    [Fact]
    public void BatchSize_CanBeOverridden()
    {
        ReEncryptionOptions options = new() { BatchSize = 1000 };

        options.BatchSize.ShouldBe(1000);
    }

    [Fact]
    public void RetiredKeyVersions_CanBePopulated()
    {
        ReEncryptionOptions options = new();
        options.RetiredKeyVersions.Add("v1");
        options.RetiredKeyVersions.Add("v2");

        options.RetiredKeyVersions.Count.ShouldBe(2);
        options.RetiredKeyVersions.ShouldContain("v1");
        options.RetiredKeyVersions.ShouldContain("v2");
    }
}
