using Granit.Core.Modularity;
using Granit.Encryption;
using Granit.Vault.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Vault;

/// <summary>
/// Abstraction module for vault services (transit encryption, dynamic credentials).
/// Install a provider module for the implementation:
/// <list type="bullet">
///   <item><c>GranitVaultHashiCorpModule</c> — HashiCorp Vault</item>
///   <item><c>GranitVaultAzureModule</c> — Azure Key Vault</item>
///   <item><c>GranitVaultAwsModule</c> — AWS KMS + Secrets Manager</item>
/// </list>
/// </summary>
/// <remarks>
/// Localization resources (<c>Localization/Vault/{culture}.json</c>) are embedded in this
/// assembly and auto-discovered by <c>GranitLocalizationModule</c> via
/// <see cref="VaultLocalizationResource"/>.
/// </remarks>
[DependsOn(typeof(GranitEncryptionModule))]
public sealed class GranitVaultModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddSingleton<VaultMetrics>();
}
