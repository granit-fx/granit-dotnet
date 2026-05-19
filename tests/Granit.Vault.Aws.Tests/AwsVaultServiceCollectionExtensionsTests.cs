using Amazon.KeyManagementService;
using Amazon.SecretsManager;
using Granit.Encryption;
using Granit.Vault.Aws.Extensions;
using Granit.Vault.Aws.HealthChecks;
using Granit.Vault.Aws.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Vault.Aws.Tests;

public sealed class AwsVaultServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitVaultAws_RegistersAwsVaultOptions()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAws();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<AwsVaultOptions>));
    }

    [Fact]
    public void AddGranitVaultAws_RegistersOptionsValidator()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAws();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<AwsVaultOptions>));
    }

    [Fact]
    public void AddGranitVaultAws_RegistersKmsClient()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAws();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAmazonKeyManagementService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultAws_RegistersSecretsManagerClient()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAws();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAmazonSecretsManager) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultAws_RegistersKmsTransitEncryption()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAws();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITransitEncryptionService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultAws_RegistersStringEncryptionProvider()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAws();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IStringEncryptionProvider) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultAws_RegistersCredentialProvider()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAws();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IDatabaseCredentialProvider) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitVaultAws_RegistersHostedService()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAws();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddGranitVaultAws_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitVaultAws();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitKmsHealthCheck_RegistersHealthCheck()
    {
        ServiceCollection services = new();
        services.AddGranitVaultAws();
        services.AddHealthChecks().AddGranitKmsHealthCheck();

        services.ShouldContain(d =>
            d.ServiceType == typeof(KmsHealthCheck));
    }
}
