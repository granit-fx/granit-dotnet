using Granit.Core.Diagnostics;
using Granit.Encryption;
using Granit.Vault.HashiCorp.Diagnostics;
using Granit.Vault.HashiCorp.HealthChecks;
using Granit.Vault.HashiCorp.Options;
using Granit.Vault.HashiCorp.Providers;
using Granit.Vault.HashiCorp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VaultSharp;

namespace Granit.Vault.HashiCorp.Extensions;

/// <summary>
/// Extensions for configuring HashiCorp Vault services in the DI container.
/// </summary>
public static class HashiCorpVaultServiceCollectionExtensions
{
    /// <summary>
    /// Adds the HashiCorp Vault client, the dynamic credentials lease manager,
    /// and the Transit encryption service.
    /// </summary>
    public static IServiceCollection AddGranitVaultHashiCorp(
        this IServiceCollection services)
    {
        services
            .AddOptions<HashiCorpVaultOptions>()
            .BindConfiguration(HashiCorpVaultOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<VaultClientFactory>();
        services.AddSingleton<IVaultClient>(sp => sp.GetRequiredService<VaultClientFactory>().Create());

        services.AddSingleton<VaultCredentialLeaseManager>();
        services.AddSingleton<IDatabaseCredentialProvider>(sp =>
            sp.GetRequiredService<VaultCredentialLeaseManager>());
        services.AddHostedService(sp => sp.GetRequiredService<VaultCredentialLeaseManager>());

        services.AddScoped<ITransitEncryptionService, HashiCorpTransitEncryptionService>();

        // String encryption provider (synchronous bridge)
        services.AddSingleton<IStringEncryptionProvider, HashiCorpVaultStringEncryptionProvider>();

        // Per-entity key isolation for crypto-shredding (GDPR Art. 17)
        services.TryAddScoped<IEntityEncryptionKeyStore, HashiCorpEntityEncryptionKeyStore>();

        GranitActivitySourceRegistry.Register(VaultHashiCorpActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Adds a HashiCorp Vault connectivity health check tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// Verifies the Vault <c>sys/health</c> endpoint. Returns <c>Degraded</c> for standby
    /// replicas (read-only but functional) and <c>Unhealthy</c> for sealed or unreachable Vault.
    /// </summary>
    public static IHealthChecksBuilder AddGranitVaultHashiCorpHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "vault",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<VaultHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<VaultHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
