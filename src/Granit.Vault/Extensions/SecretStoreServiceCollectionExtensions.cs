using Granit.Vault.Diagnostics;
using Granit.Vault.Internal;
using Granit.Vault.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Vault.Extensions;

/// <summary>
/// Helpers for provider-specific packages to register their <see cref="ISecretStore"/>
/// implementation with the optional FusionCache decorator applied transparently.
/// </summary>
public static class SecretStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TStore"/> as the <see cref="ISecretStore"/>
    /// implementation. When <c>Vault:SecretStore:CacheSeconds &gt; 0</c> at service
    /// resolution time, the concrete store is wrapped in a <c>CachedSecretStore</c>
    /// decorator that adds FusionCache caching and emits the
    /// <c>granit.vault.secret.read</c> metric.
    /// </summary>
    /// <typeparam name="TStore">Concrete provider implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="providerName">
    /// Provider tag used for metrics and cache key prefixing
    /// (e.g. <c>"hashicorp"</c>, <c>"azure"</c>, <c>"aws"</c>, <c>"gcp"</c>).
    /// </param>
    public static IServiceCollection AddGranitSecretStore<TStore>(
        this IServiceCollection services,
        string providerName)
        where TStore : class, ISecretStore
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        // Self-sufficient registration: ensure VaultMetrics and SecretStoreOptions are available
        // even when the extension is called outside the GranitVaultModule pipeline (e.g. tests
        // that stand up a ServiceCollection manually).
        services.TryAddSingleton<VaultMetrics>();
        services
            .AddOptions<SecretStoreOptions>()
            .BindConfiguration(SecretStoreOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddSingleton<TStore>();

        services.AddSingleton<ISecretStore>(sp =>
        {
            SecretStoreOptions options = sp.GetRequiredService<IOptions<SecretStoreOptions>>().Value;
            TStore inner = sp.GetRequiredService<TStore>();

            if (options.CacheSeconds <= 0)
            {
                return new ProviderTaggingSecretStore(
                    inner,
                    sp.GetRequiredService<VaultMetrics>(),
                    sp.GetRequiredService<IServiceProvider>(),
                    providerName);
            }

            return new CachedSecretStore(
                inner,
                sp.GetRequiredService<IFusionCache>(),
                sp.GetRequiredService<VaultMetrics>(),
                sp.GetRequiredService<IOptions<SecretStoreOptions>>(),
                sp,
                sp.GetRequiredService<ILogger<CachedSecretStore>>(),
                providerName);
        });

        return services;
    }
}
