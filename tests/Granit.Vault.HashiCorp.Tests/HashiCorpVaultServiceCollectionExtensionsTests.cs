using Granit.Caching.Extensions;
using Granit.Vault.HashiCorp.Extensions;
using Granit.Vault.HashiCorp.HealthChecks;
using Granit.Vault.HashiCorp.Options;
using Granit.Vault.HashiCorp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using VaultSharp;
using Xunit;

namespace Granit.Vault.HashiCorp.Tests;

public sealed class HashiCorpVaultServiceCollectionExtensionsTests
{
    private static IConfiguration CreateVaultConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vault:Address"] = "https://vault.test.com",
                ["Vault:AuthMethod"] = "Token",
                ["Vault:Token"] = "test-token",
                ["Vault:DatabaseMountPoint"] = "database",
                ["Vault:DatabaseRoleName"] = "readwrite"
            })
            .Build();

    [Fact]
    public void AddGranitVaultHashiCorp_RegistersVaultOptions()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(CreateVaultConfiguration());

        services.AddGranitVaultHashiCorp();

        using ServiceProvider sp = services.BuildServiceProvider();

        HashiCorpVaultOptions options = sp.GetRequiredService<IOptions<HashiCorpVaultOptions>>().Value;
        options.Address.ShouldBe("https://vault.test.com");
        options.AuthMethod.ShouldBe("Token");
        options.DatabaseRoleName.ShouldBe("readwrite");
    }

    [Fact]
    public void AddGranitVaultHashiCorp_RegistersVaultClient()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(CreateVaultConfiguration());

        services.AddGranitVaultHashiCorp();

        using ServiceProvider sp = services.BuildServiceProvider();

        IVaultClient? client = sp.GetService<IVaultClient>();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitVaultHashiCorp_RegistersDatabaseCredentialProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(CreateVaultConfiguration());

        services.AddGranitVaultHashiCorp();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IDatabaseCredentialProvider));

        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultHashiCorp_RegistersHostedService()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(CreateVaultConfiguration());

        services.AddGranitVaultHashiCorp();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IHostedService));

        descriptor.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitVaultHashiCorp_RegistersTransitEncryptionService()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(CreateVaultConfiguration());

        services.AddGranitVaultHashiCorp();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ITransitEncryptionService));

        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(HashiCorpTransitEncryptionService));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitVaultHashiCorpHealthCheck_RegistersVaultHealthCheck_AsReadinessCheck()
    {
        ServiceCollection services = new();
        services.AddSingleton(Substitute.For<IVaultClient>());
        IHealthChecksBuilder builder = services.AddHealthChecks();

        builder.AddGranitVaultHashiCorpHealthCheck();

        ServiceDescriptor? healthCheckDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(VaultHealthCheck));
        healthCheckDescriptor.ShouldNotBeNull();
        healthCheckDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);

        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions opts = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        HealthCheckRegistration? registration = opts.Registrations.FirstOrDefault(r => r.Name == "vault");
        registration.ShouldNotBeNull();
        registration!.Tags.ShouldContain("readiness");
        registration.Tags.ShouldContain("startup");
    }

    [Fact]
    public void AddGranitVaultHashiCorp_RegistersSecretStore_WithoutCache_ByDefault()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton<IConfiguration>(CreateVaultConfiguration());
        services.AddGranitCaching();

        services.AddGranitVaultHashiCorp();

        using ServiceProvider sp = services.BuildServiceProvider();

        ISecretStore store = sp.GetRequiredService<ISecretStore>();
        store.ShouldNotBeNull();
        // SecretCacheSeconds defaults to 0 → no FusionCache decorator is inserted.
        store.GetType().Name.ShouldBe("ProviderTaggingSecretStore");
    }

    [Fact]
    public void AddGranitVaultHashiCorp_WithCacheEnabled_WrapsInCachedDecorator()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMetrics();

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vault:Address"] = "https://vault.test.com",
                ["Vault:AuthMethod"] = "Token",
                ["Vault:Token"] = "test-token",
                ["Vault:SecretStore:CacheSeconds"] = "60",
            })
            .Build();
        services.AddSingleton(config);
        services.AddGranitCaching();

        services.AddGranitVaultHashiCorp();

        using ServiceProvider sp = services.BuildServiceProvider();

        ISecretStore store = sp.GetRequiredService<ISecretStore>();
        store.GetType().Name.ShouldBe("CachedSecretStore");
    }

    [Fact]
    public void AddGranitVaultHashiCorpHealthCheck_WithCustomName_RegistersCheckWithThatName()
    {
        ServiceCollection services = new();
        services.AddSingleton(Substitute.For<IVaultClient>());
        IHealthChecksBuilder builder = services.AddHealthChecks();

        builder.AddGranitVaultHashiCorpHealthCheck(name: "vault-primary");

        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions opts = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        HealthCheckRegistration? registration = opts.Registrations.FirstOrDefault(r => r.Name == "vault-primary");
        registration.ShouldNotBeNull();
    }
}
