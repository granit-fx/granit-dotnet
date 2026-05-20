using Granit.Caching.Vault.Options;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Granit.Caching.Vault.Tests;

public sealed class GranitCachingVaultModuleTests
{
    [Fact]
    public void IsEnabled_WithoutSecretName_ReturnsFalse()
    {
        CacheEncryptionVaultOptions? opts = BuildConfig(
            new Dictionary<string, string?>())
            .GetSection(CacheEncryptionVaultOptions.SectionName)
            .Get<CacheEncryptionVaultOptions>();

        string.IsNullOrWhiteSpace(opts?.SecretName).ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_WithSecretName_BindsCorrectly()
    {
        CacheEncryptionVaultOptions? opts = BuildConfig(
            new Dictionary<string, string?>
            {
                ["Cache:Encryption:Vault:SecretName"] = "granit/cache/encryption-key",
                ["Cache:Encryption:Vault:SecretVersion"] = "7",
            })
            .GetSection(CacheEncryptionVaultOptions.SectionName)
            .Get<CacheEncryptionVaultOptions>();

        opts.ShouldNotBeNull();
        opts!.SecretName.ShouldBe("granit/cache/encryption-key");
        opts.SecretVersion.ShouldBe("7");
    }

    [Fact]
    public void SectionName_IsCacheEncryptionVault() =>
        CacheEncryptionVaultOptions.SectionName.ShouldBe("Cache:Encryption:Vault");

    private static IConfiguration BuildConfig(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
