using Granit.Caching.Vault.Extensions;
using Granit.Caching.Vault.Options;
using Granit.Modularity;
using Granit.Vault;
using Microsoft.Extensions.Configuration;

namespace Granit.Caching.Vault;

/// <summary>
/// Granit module that bridges <see cref="ISecretStore"/> to
/// <c>Cache:Encryption:Key</c>. Active only when
/// <c>Cache:Encryption:Vault:SecretName</c> is configured — Development typically
/// leaves it empty and uses a static <c>Cache:Encryption:Key</c> instead.
/// </summary>
/// <remarks>
/// <para>
/// Pair with <c>GranitCachingStackExchangeRedisModule</c> and an active
/// <c>ISecretStore</c> provider (<c>Granit.Vault.HashiCorp</c>,
/// <c>.Azure</c>, <c>.Aws</c>, or <c>.GoogleCloud</c>). The bridge is
/// provider-agnostic.
/// </para>
/// <para>
/// Example <c>appsettings.json</c>:
/// <code>
/// {
///   "Cache": {
///     "EncryptValues": true,
///     "Encryption": {
///       "Vault": { "SecretName": "granit/cache/encryption-key" }
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
[DependsOn(typeof(GranitCachingModule), typeof(GranitVaultModule))]
public sealed class GranitCachingVaultModule : GranitModule
{
    /// <inheritdoc/>
    public override bool IsEnabled(ServiceConfigurationContext context)
    {
        CacheEncryptionVaultOptions? opts = context.Configuration
            .GetSection(CacheEncryptionVaultOptions.SectionName)
            .Get<CacheEncryptionVaultOptions>();

        return !string.IsNullOrWhiteSpace(opts?.SecretName);
    }

    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCachingEncryptionFromVault();
}
