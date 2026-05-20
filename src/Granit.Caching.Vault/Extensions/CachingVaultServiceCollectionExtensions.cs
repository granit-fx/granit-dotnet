using Granit.Caching.Options;
using Granit.Caching.Vault.Internal;
using Granit.Caching.Vault.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Caching.Vault.Extensions;

/// <summary>
/// DI registration extensions for the Granit.Caching ↔ Granit.Vault bridge.
/// </summary>
public static class CachingVaultServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="IPostConfigureOptions{CacheEncryptionOptions}"/> that
    /// hydrates the AES key from <see cref="Granit.Vault.ISecretStore"/> at host start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="CacheEncryptionVaultOptions"/> from configuration section
    /// <c>"Cache:Encryption:Vault"</c>. The bridge is fail-closed: if Vault is unreachable
    /// or <c>Cache:EncryptValues</c> is <c>false</c>, the host fails to start.
    /// </para>
    /// <para>
    /// Idempotent — calling multiple times registers the post-configure once.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitCachingEncryptionFromVault(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<CacheEncryptionVaultOptions>()
            .BindConfiguration(CacheEncryptionVaultOptions.SectionName);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<CacheEncryptionOptions>, VaultCacheEncryptionPostConfigure>());

        return services;
    }
}
