using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;
using Granit.Diagnostics;
using Granit.Encryption;
using Granit.Vault.Azure.Diagnostics;
using Granit.Vault.Azure.HealthChecks;
using Granit.Vault.Azure.Options;
using Granit.Vault.Azure.Providers;
using Granit.Vault.Azure.Services;
using Granit.Vault.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Azure.Extensions;

/// <summary>Extension methods for the Azure Key Vault provider.</summary>
public static class AzureKeyVaultServiceCollectionExtensions
{
    /// <summary>Registers Azure Key Vault encryption and secrets credential provider.</summary>
    public static IServiceCollection AddGranitVaultAzure(this IServiceCollection services)
    {
        services.AddOptions<AzureKeyVaultOptions>()
            .BindConfiguration(AzureKeyVaultOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AzureKeyVaultOptions>, AzureKeyVaultOptionsValidator>();

        // Key Vault KeyClient
        services.AddSingleton<KeyClient>(sp =>
        {
            AzureKeyVaultOptions opts = sp.GetRequiredService<IOptions<AzureKeyVaultOptions>>().Value;
            return new KeyClient(new Uri(opts.VaultUri), new DefaultAzureCredential());
        });

        // Key Vault SecretClient
        services.AddSingleton<SecretClient>(sp =>
        {
            AzureKeyVaultOptions opts = sp.GetRequiredService<IOptions<AzureKeyVaultOptions>>().Value;
            return new SecretClient(new Uri(opts.VaultUri), new DefaultAzureCredential());
        });

        // CryptographyClient for the configured key
        services.AddSingleton<CryptographyClient>(sp =>
        {
            AzureKeyVaultOptions opts = sp.GetRequiredService<IOptions<AzureKeyVaultOptions>>().Value;
            var keyUri = new Uri($"{opts.VaultUri.TrimEnd('/')}/keys/{opts.EncryptionKeyName}");
            return new CryptographyClient(keyUri, new DefaultAzureCredential());
        });

        // Azure Key Vault transit encryption
        services.AddSingleton<ITransitEncryptionService, AzureKeyVaultTransitEncryptionService>();

        // String encryption provider (synchronous bridge)
        services.AddSingleton<IStringEncryptionProvider, AzureKeyVaultStringEncryptionProvider>();

        // Database credential provider (BackgroundService)
        services.AddSingleton<AzureSecretsCredentialProvider>();
        services.AddSingleton<IDatabaseCredentialProvider>(sp =>
            sp.GetRequiredService<AzureSecretsCredentialProvider>());
        services.AddHostedService(sp => sp.GetRequiredService<AzureSecretsCredentialProvider>());

        // ISecretStore for arbitrary secret retrieval
        services.AddGranitSecretStore<AzureSecretStore>("azure");

        GranitActivitySourceRegistry.Register(VaultAzureActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Registers the Managed HSM-backed <see cref="ITransitMacService"/>. Call AFTER
    /// <see cref="AddGranitVaultAzure"/>; Managed HSM is a separate Azure resource so
    /// the host configures it as an opt-in for callers (privacy, webhook signing) that
    /// need MAC.
    /// </summary>
    /// <remarks>
    /// Azure Key Vault Standard tier has no HMAC primitive — register
    /// <see cref="Granit.Vault.Extensions.SecretBackedMacServiceCollectionExtensions.AddGranitSecretBackedMacService"/>
    /// instead, with the secret stored in the standard Key Vault.
    /// </remarks>
    public static IServiceCollection AddGranitVaultAzureManagedHsmMac(this IServiceCollection services)
    {
        services.AddOptions<AzureManagedHsmMacOptions>()
            .BindConfiguration(AzureManagedHsmMacOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ITransitMacService, AzureManagedHsmMacService>();
        return services;
    }

    /// <summary>Adds an Azure Key Vault health check.</summary>
    public static IHealthChecksBuilder AddGranitAzureKeyVaultHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "azure-key-vault",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<AzureKeyVaultHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<AzureKeyVaultHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
