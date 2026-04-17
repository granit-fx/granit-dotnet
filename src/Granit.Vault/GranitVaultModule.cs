using Granit.Caching;
using Granit.Encryption;
using Granit.Modularity;
using Granit.Vault.Diagnostics;
using Granit.Vault.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Vault;

/// <summary>
/// Abstraction module for vault services (transit encryption, dynamic credentials, secret store).
/// Install a provider module for the implementation:
/// <list type="bullet">
///   <item><c>GranitVaultHashiCorpModule</c> — HashiCorp Vault</item>
///   <item><c>GranitVaultAzureModule</c> — Azure Key Vault</item>
///   <item><c>GranitVaultAwsModule</c> — AWS KMS + Secrets Manager</item>
///   <item><c>GranitVaultGoogleCloudModule</c> — Google Cloud KMS + Secret Manager</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Localization resources (<c>Localization/Vault/{culture}.json</c>) are embedded in this
/// assembly and auto-discovered by <c>GranitLocalizationModule</c> via
/// <see cref="VaultLocalizationResource"/>.
/// </para>
/// <para>
/// Depends on <c>GranitCachingModule</c> to make <see cref="ISecretStore"/> caching
/// transparent. The cache decorator is only wired when <c>Vault:SecretStore:CacheSeconds</c>
/// is greater than zero; when disabled (default), FusionCache is a zero-cost transitive
/// dependency.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitCachingModule), typeof(GranitEncryptionModule))]
public sealed class GranitVaultModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<VaultMetrics>();

        context.Services
            .AddOptions<SecretStoreOptions>()
            .BindConfiguration(SecretStoreOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
