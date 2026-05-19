using Granit.Authentication.JwtBearer.Keycloak.Extensions;
using Granit.Modularity;

namespace Granit.Authentication.JwtBearer.Keycloak;

/// <summary>
/// Granit module for Keycloak extras (claims transformation, Admin policy).
/// Depends on <see cref="GranitAuthenticationJwtBearerModule"/> for generic JWT Bearer.
/// </summary>
[DependsOn(typeof(GranitAuthenticationJwtBearerModule))]
public sealed class GranitAuthenticationJwtBearerKeycloakModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitKeycloak();
}
