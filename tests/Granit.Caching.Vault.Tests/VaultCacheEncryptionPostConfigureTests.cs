using System.Security.Cryptography;
using Granit.Caching.Options;
using Granit.Caching.Vault.Extensions;
using Granit.Vault;
using Granit.Vault.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Caching.Vault.Tests;

public sealed class VaultCacheEncryptionPostConfigureTests
{
    private const string SampleSecretName = "granit/cache/encryption-key";

    [Fact]
    public void PostConfigure_WithVaultSecret_HydratesKey()
    {
        string base64Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        ISecretStore secretStore = Substitute.For<ISecretStore>();
        secretStore
            .GetSecretAsync(Arg.Any<SecretRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SecretDescriptor.FromString(SampleSecretName, base64Key)));

        ServiceProvider sp = BuildProvider(
            new Dictionary<string, string?>
            {
                ["Cache:EncryptValues"] = "true",
                ["Cache:Encryption:Vault:SecretName"] = SampleSecretName,
            },
            secretStore);

        CacheEncryptionOptions options = sp.GetRequiredService<IOptions<CacheEncryptionOptions>>().Value;

        options.Key.ShouldBe(base64Key);
    }

    [Fact]
    public void PostConfigure_WithSpecificVersion_RequestsThatVersion()
    {
        string base64Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        SecretRequest? captured = null;

        ISecretStore secretStore = Substitute.For<ISecretStore>();
        secretStore
            .GetSecretAsync(Arg.Do<SecretRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SecretDescriptor.FromString(SampleSecretName, base64Key)));

        ServiceProvider sp = BuildProvider(
            new Dictionary<string, string?>
            {
                ["Cache:EncryptValues"] = "true",
                ["Cache:Encryption:Vault:SecretName"] = SampleSecretName,
                ["Cache:Encryption:Vault:SecretVersion"] = "42",
            },
            secretStore);

        _ = sp.GetRequiredService<IOptions<CacheEncryptionOptions>>().Value;

        captured.ShouldNotBeNull();
        captured!.Name.ShouldBe(SampleSecretName);
        captured.Version!.Identifier.ShouldBe("42");
    }

    [Fact]
    public void PostConfigure_WhenEncryptValuesIsFalse_Throws()
    {
        ServiceProvider sp = BuildProvider(
            new Dictionary<string, string?>
            {
                ["Cache:EncryptValues"] = "false",
                ["Cache:Encryption:Vault:SecretName"] = SampleSecretName,
            },
            secretStore: Substitute.For<ISecretStore>());

        Action act = () => _ = sp.GetRequiredService<IOptions<CacheEncryptionOptions>>().Value;

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(act);
        ex.Message.ShouldContain("Cache:EncryptValues=false");
    }

    [Fact]
    public void PostConfigure_WhenSecretStoreMissing_Throws()
    {
        ServiceProvider sp = BuildProvider(
            new Dictionary<string, string?>
            {
                ["Cache:EncryptValues"] = "true",
                ["Cache:Encryption:Vault:SecretName"] = SampleSecretName,
            },
            secretStore: null);

        Action act = () => _ = sp.GetRequiredService<IOptions<CacheEncryptionOptions>>().Value;

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(act);
        ex.Message.ShouldContain("ISecretStore");
        ex.Message.ShouldContain("Vault provider package");
    }

    [Fact]
    public void PostConfigure_WhenSecretNameUnset_IsNoOp()
    {
        ServiceProvider sp = BuildProvider(
            new Dictionary<string, string?>
            {
                ["Cache:EncryptValues"] = "true",
                ["Cache:Encryption:Key"] = "static-dev-key",
            },
            secretStore: null);

        CacheEncryptionOptions options = sp.GetRequiredService<IOptions<CacheEncryptionOptions>>().Value;

        options.Key.ShouldBe("static-dev-key");
    }

    [Fact]
    public void PostConfigure_WhenVaultThrowsTransient_Propagates()
    {
        ISecretStore secretStore = Substitute.For<ISecretStore>();
        secretStore
            .GetSecretAsync(Arg.Any<SecretRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<SecretDescriptor>>(_ => throw new SecretVaultTransientException(SampleSecretName, "vault down"));

        ServiceProvider sp = BuildProvider(
            new Dictionary<string, string?>
            {
                ["Cache:EncryptValues"] = "true",
                ["Cache:Encryption:Vault:SecretName"] = SampleSecretName,
            },
            secretStore);

        Should.Throw<SecretVaultTransientException>(
            () => _ = sp.GetRequiredService<IOptions<CacheEncryptionOptions>>().Value);
    }

    private static ServiceProvider BuildProvider(
        IDictionary<string, string?> config,
        ISecretStore? secretStore)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        // Mirror Granit.Caching's registration of CacheEncryptionOptions + CachingOptions.
        services.AddOptions<CacheEncryptionOptions>().BindConfiguration(CacheEncryptionOptions.SectionName);
        services.AddOptions<CachingOptions>().BindConfiguration(CachingOptions.SectionName);

        if (secretStore is not null)
        {
            services.AddSingleton(secretStore);
        }

        services.AddGranitCachingEncryptionFromVault();

        return services.BuildServiceProvider();
    }
}
