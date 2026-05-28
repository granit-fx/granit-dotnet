using Granit.Vault.Options;
using Granit.Vault.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Vault.Extensions;

/// <summary>
/// DI extensions for the portable secret-backed MAC service.
/// </summary>
public static class SecretBackedMacServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SecretBackedMacService"/> as the active
    /// <see cref="ITransitMacService"/>. The 32-byte key is loaded from the configured
    /// <see cref="ISecretStore"/> at startup (and refreshed periodically by a hosted service).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Trust-boundary trade-off:</b> the key material lives in process memory while
    /// the service is alive. Memory is zeroed on dispose and on every refresh. Prefer a
    /// native HMAC primitive (HashiCorp Transit, AWS KMS HMAC, GCP KMS MAC, Azure Managed
    /// HSM) when one is available — this implementation exists for Azure Standard tier
    /// and cost-constrained deployments.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGranitSecretBackedMacService(
        this IServiceCollection services,
        Action<SecretBackedMacOptions>? configure = null)
    {
        services
            .AddOptions<SecretBackedMacOptions>()
            .BindConfiguration(SecretBackedMacOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton<SecretBackedMacService>();
        services.TryAddSingleton<ITransitMacService>(sp => sp.GetRequiredService<SecretBackedMacService>());
        services.AddHostedService<SecretBackedMacRefreshHostedService>();
        return services;
    }
}
