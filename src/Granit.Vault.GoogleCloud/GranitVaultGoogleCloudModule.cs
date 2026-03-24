using Granit.Modularity;
using Granit.Vault.GoogleCloud.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Vault.GoogleCloud;

/// <summary>Module for Google Cloud KMS + Secret Manager vault provider.</summary>
[DependsOn(typeof(GranitVaultModule))]
public sealed class GranitVaultGoogleCloudModule : GranitModule
{
    /// <inheritdoc />
    public override bool IsEnabled(ServiceConfigurationContext context) =>
        !context.Builder.Environment.IsDevelopment();

    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitVaultGoogleCloud();
}
