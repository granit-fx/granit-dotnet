using Granit.Modularity;
using Granit.Vault.Aws.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Vault.Aws;

/// <summary>Module for AWS KMS + Secrets Manager vault provider.</summary>
[DependsOn(typeof(GranitVaultModule))]
public sealed class GranitVaultAwsModule : GranitModule
{
    /// <inheritdoc />
    public override bool IsEnabled(ServiceConfigurationContext context) =>
        !context.Builder.Environment.IsDevelopment();

    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitVaultAws();
}
