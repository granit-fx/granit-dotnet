using Granit.Modularity;
using Granit.Vault.Azure.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Vault.Azure;

/// <summary>Module for Azure Key Vault encryption and secrets provider.</summary>
[DependsOn(typeof(GranitVaultModule))]
public sealed class GranitVaultAzureModule : GranitModule
{
    /// <inheritdoc />
    public override bool IsEnabled(ServiceConfigurationContext context) =>
        !context.Builder.Environment.IsDevelopment();

    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitVaultAzure();
}
