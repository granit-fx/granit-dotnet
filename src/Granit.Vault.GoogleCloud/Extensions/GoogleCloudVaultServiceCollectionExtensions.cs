using Google.Apis.Auth.OAuth2;
using Google.Cloud.Kms.V1;
using Google.Cloud.SecretManager.V1;
using Granit.Core.Diagnostics;
using Granit.Encryption;
using Granit.Vault.GoogleCloud.Diagnostics;
using Granit.Vault.GoogleCloud.HealthChecks;
using Granit.Vault.GoogleCloud.Options;
using Granit.Vault.GoogleCloud.Providers;
using Granit.Vault.GoogleCloud.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Vault.GoogleCloud.Extensions;

/// <summary>Extension methods for the Google Cloud Vault provider.</summary>
public static class GoogleCloudVaultServiceCollectionExtensions
{
    /// <summary>Registers Google Cloud KMS encryption and Secret Manager credential provider.</summary>
    public static IServiceCollection AddGranitVaultGoogleCloud(this IServiceCollection services)
    {
        services.AddOptions<GoogleCloudVaultOptions>()
            .BindConfiguration(GoogleCloudVaultOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<GoogleCloudVaultOptions>, GoogleCloudVaultOptionsValidator>();

        // Cloud KMS client
        services.AddSingleton(sp =>
        {
            GoogleCloudVaultOptions opts = sp.GetRequiredService<IOptions<GoogleCloudVaultOptions>>().Value;

            if (!string.IsNullOrEmpty(opts.CredentialFilePath))
            {
                ServiceAccountCredential credential = CredentialFactory.FromFile<ServiceAccountCredential>(opts.CredentialFilePath);
                KeyManagementServiceClientBuilder builder = new()
                {
                    GoogleCredential = credential.ToGoogleCredential(),
                };
                return builder.Build();
            }

            return KeyManagementServiceClient.Create();
        });

        // Secret Manager client
        services.AddSingleton(sp =>
        {
            GoogleCloudVaultOptions opts = sp.GetRequiredService<IOptions<GoogleCloudVaultOptions>>().Value;

            if (!string.IsNullOrEmpty(opts.CredentialFilePath))
            {
                ServiceAccountCredential credential = CredentialFactory.FromFile<ServiceAccountCredential>(opts.CredentialFilePath);
                SecretManagerServiceClientBuilder builder = new()
                {
                    GoogleCredential = credential.ToGoogleCredential(),
                };
                return builder.Build();
            }

            return SecretManagerServiceClient.Create();
        });

        // Cloud KMS transit encryption
        services.AddSingleton<ITransitEncryptionService, CloudKmsTransitEncryptionService>();

        // String encryption provider (synchronous bridge)
        services.AddSingleton<IStringEncryptionProvider, CloudKmsStringEncryptionProvider>();

        // Database credential provider (BackgroundService)
        services.AddSingleton<SecretManagerCredentialProvider>();
        services.AddSingleton<IDatabaseCredentialProvider>(sp =>
            sp.GetRequiredService<SecretManagerCredentialProvider>());
        services.AddHostedService(sp => sp.GetRequiredService<SecretManagerCredentialProvider>());

        GranitActivitySourceRegistry.Register(VaultGoogleCloudActivitySource.Name);

        return services;
    }

    /// <summary>Adds a Cloud KMS health check.</summary>
    public static IHealthChecksBuilder AddGranitCloudKmsHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "gcp-kms",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<CloudKmsHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<CloudKmsHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
