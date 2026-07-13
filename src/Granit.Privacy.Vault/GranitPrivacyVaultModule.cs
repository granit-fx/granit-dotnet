using Granit.Modularity;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.Vault.Extensions;
using Granit.Vault;

namespace Granit.Privacy.Vault;

/// <summary>
/// Wires the Vault-backed <see cref="VaultExportHmacSigner"/> into the privacy export
/// pipeline. Requires a registered <see cref="ITransitMacService"/> — typically from
/// <c>GranitVaultHashiCorpModule</c> / <c>GranitVaultAwsModule</c> /
/// <c>GranitVaultGoogleCloudModule</c> / <c>GranitVaultAzureModule</c>, or the
/// portable <c>SecretBackedMacService</c>.
/// </summary>
[DependsOn(typeof(GranitPrivacyBlobStorageModule))]
[DependsOn(typeof(GranitPrivacyModule))]
[DependsOn(typeof(GranitVaultModule))]
public sealed class GranitPrivacyVaultModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitPrivacyVaultExportSigner();
}
