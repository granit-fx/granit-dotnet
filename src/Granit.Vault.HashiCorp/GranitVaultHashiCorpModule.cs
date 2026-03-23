using Granit.Core.Modularity;
using Granit.Vault.HashiCorp.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Vault.HashiCorp;

/// <summary>
/// HashiCorp Vault provider module — Transit encryption and dynamic database credentials.
/// Disabled in Development (no Vault required locally).
/// </summary>
[DependsOn(typeof(GranitVaultModule))]
public sealed class GranitVaultHashiCorpModule : GranitModule
{
    /// <inheritdoc />
    public override bool IsEnabled(ServiceConfigurationContext context) =>
        !context.Builder.Environment.IsDevelopment();

    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitVaultHashiCorp();
}
