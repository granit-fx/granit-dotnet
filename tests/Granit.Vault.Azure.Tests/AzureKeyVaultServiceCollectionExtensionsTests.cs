using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;
using Granit.Encryption;
using Granit.Vault.Azure.Extensions;
using Granit.Vault.Azure.HealthChecks;
using Granit.Vault.Azure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Vault.Azure.Tests;

public sealed class AzureKeyVaultServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitVaultAzure_RegistersAzureKeyVaultOptions()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAzure();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<AzureKeyVaultOptions>));
    }

    [Fact]
    public void AddGranitVaultAzure_RegistersOptionsValidator()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAzure();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<AzureKeyVaultOptions>));
    }

    [Fact]
    public void AddGranitVaultAzure_RegistersKeyClient()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAzure();

        services.ShouldContain(d =>
            d.ServiceType == typeof(KeyClient) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultAzure_RegistersSecretClient()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAzure();

        services.ShouldContain(d =>
            d.ServiceType == typeof(SecretClient) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultAzure_RegistersCryptographyClient()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAzure();

        services.ShouldContain(d =>
            d.ServiceType == typeof(CryptographyClient) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultAzure_RegistersAzureKeyVaultTransitEncryption()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAzure();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITransitEncryptionService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultAzure_RegistersStringEncryptionProvider()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAzure();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IStringEncryptionProvider) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultAzure_RegistersCredentialProvider()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAzure();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IDatabaseCredentialProvider) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultAzure_RegistersHostedService()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAzure();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddGranitVaultAzure_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitVaultAzure();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitAzureKeyVaultHealthCheck_RegistersHealthCheck()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAzure();
        services.AddHealthChecks().AddGranitAzureKeyVaultHealthCheck();

        services.ShouldContain(d =>
            d.ServiceType == typeof(AzureKeyVaultHealthCheck));
    }
}
