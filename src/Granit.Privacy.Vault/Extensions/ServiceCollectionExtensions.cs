using Granit.Privacy.DataExport.Security;
using Granit.Privacy.Vault.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Privacy.Vault.Extensions;

/// <summary>DI extensions for the Vault-backed privacy export signer.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default <see cref="EphemeralExportHmacSigner"/> registered by
    /// <c>Granit.Privacy.BlobStorage</c> with <see cref="VaultExportHmacSigner"/>.
    /// Requires a <see cref="Granit.Vault.ITransitMacService"/> to be registered by
    /// any Vault provider module installed in the host.
    /// </summary>
    /// <remarks>
    /// Call this AFTER <c>AddGranitPrivacyBlobStorage()</c> — the implementation uses
    /// <c>services.Replace</c> rather than <c>TryAdd</c> because the BlobStorage
    /// package always registers the ephemeral signer, so without an explicit replace
    /// the Vault-backed signer would silently lose to it.
    /// </remarks>
    public static IServiceCollection AddGranitPrivacyVaultExportSigner(
        this IServiceCollection services,
        Action<VaultExportSignerOptions>? configure = null)
    {
        services
            .AddOptions<VaultExportSignerOptions>()
            .BindConfiguration(VaultExportSignerOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.AddSingleton<VaultExportHmacSigner>();
        services.Replace(ServiceDescriptor.Singleton<IExportHmacSigner>(sp =>
            sp.GetRequiredService<VaultExportHmacSigner>()));
        services.Replace(ServiceDescriptor.Singleton<IExportContentSigner>(sp =>
            sp.GetRequiredService<VaultExportHmacSigner>()));
        return services;
    }
}
