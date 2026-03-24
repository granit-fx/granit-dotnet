using Granit.Identity.Federated.GoogleCloud.Extensions;
using Granit.Modularity;

namespace Granit.Identity.Federated.GoogleCloud;

/// <summary>
/// Granit module that registers Google Cloud Identity Platform (Firebase Auth) as the
/// <see cref="IIdentityProvider"/> implementation.
/// </summary>
[DependsOn(typeof(GranitIdentityFederatedModule))]
public sealed class GranitIdentityFederatedGoogleCloudModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIdentityGoogleCloud();
}
