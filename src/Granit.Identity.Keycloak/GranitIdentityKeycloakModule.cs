using Granit.Core.Modularity;
using Granit.HttpResilience;
using Granit.Identity.Keycloak.Extensions;
using Granit.Timing;

namespace Granit.Identity.Keycloak;

/// <summary>
/// Granit module that registers the Keycloak Admin API as the
/// <see cref="IIdentityProvider"/> implementation.
/// </summary>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitIdentityModule),
    typeof(GranitTimingModule))]
public sealed class GranitIdentityKeycloakModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIdentityKeycloak();
}
