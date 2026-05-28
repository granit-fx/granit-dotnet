using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Granit.Diagnostics;
using Granit.Encryption;
using Granit.Vault.Aws.Diagnostics;
using Granit.Vault.Aws.HealthChecks;
using Granit.Vault.Aws.Options;
using Granit.Vault.Aws.Providers;
using Granit.Vault.Aws.Services;
using Granit.Vault.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Aws.Extensions;

/// <summary>Extension methods for the AWS Vault provider.</summary>
public static class AwsVaultServiceCollectionExtensions
{
    /// <summary>Registers AWS KMS encryption and Secrets Manager credential provider.</summary>
    public static IServiceCollection AddGranitVaultAws(this IServiceCollection services)
    {
        services.AddOptions<AwsVaultOptions>()
            .BindConfiguration(AwsVaultOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AwsVaultOptions>, AwsVaultOptionsValidator>();

        // KMS client
        services.AddSingleton<IAmazonKeyManagementService>(sp =>
        {
            AwsVaultOptions opts = sp.GetRequiredService<IOptions<AwsVaultOptions>>().Value;
            AmazonKeyManagementServiceConfig config = new()
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(opts.Region),
                Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds),
            };

            return opts.AccessKeyId is not null
                ? new AmazonKeyManagementServiceClient(
                    new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey), config)
                : new AmazonKeyManagementServiceClient(config);
        });

        // Secrets Manager client
        services.AddSingleton<IAmazonSecretsManager>(sp =>
        {
            AwsVaultOptions opts = sp.GetRequiredService<IOptions<AwsVaultOptions>>().Value;
            AmazonSecretsManagerConfig config = new()
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(opts.Region),
                Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds),
            };

            return opts.AccessKeyId is not null
                ? new AmazonSecretsManagerClient(
                    new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey), config)
                : new AmazonSecretsManagerClient(config);
        });

        // KMS transit encryption
        services.AddSingleton<ITransitEncryptionService, KmsTransitEncryptionService>();

        // KMS HMAC (separate options section because the rotation model is alias-based
        // rather than the single-key spec used by encryption).
        services.AddOptions<AwsKmsMacOptions>()
            .BindConfiguration(AwsKmsMacOptions.SectionName)
            .ValidateDataAnnotations();
        services.AddSingleton<ITransitMacService, AwsKmsMacService>();

        // String encryption provider (synchronous bridge)
        services.AddSingleton<IStringEncryptionProvider, KmsStringEncryptionProvider>();

        // Database credential provider (BackgroundService)
        services.AddSingleton<AwsSecretsCredentialProvider>();
        services.AddSingleton<IDatabaseCredentialProvider>(sp =>
            sp.GetRequiredService<AwsSecretsCredentialProvider>());
        services.AddHostedService(sp => sp.GetRequiredService<AwsSecretsCredentialProvider>());

        // ISecretStore for arbitrary secret retrieval
        services.AddGranitSecretStore<AwsSecretStore>("aws");

        GranitActivitySourceRegistry.Register(VaultAwsActivitySource.Name);

        return services;
    }

    /// <summary>Adds a KMS health check.</summary>
    public static IHealthChecksBuilder AddGranitKmsHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "aws-kms",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<KmsHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<KmsHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
