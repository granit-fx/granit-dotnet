using Granit.Http.Resilience;
using Granit.Identity.Federated.Keycloak.Extensions;
using Granit.Modularity;
using Granit.Timing;

namespace Granit.Identity.Federated.Keycloak;

/// <summary>
/// Granit module that registers the Keycloak Admin API as the
/// <see cref="IIdentityProvider"/> implementation.
/// </summary>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitIdentityFederatedModule),
    typeof(GranitTimingModule))]
public sealed class GranitIdentityFederatedKeycloakModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIdentityKeycloak();
}
