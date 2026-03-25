using Granit.Authentication.JwtBearer;
using Granit.Authentication.JwtBearer.Keycloak.Extensions;
using Granit.Modularity;

namespace Granit.Authentication.JwtBearer.Keycloak;

/// <summary>
/// Granit module for Keycloak extras (claims transformation, Admin policy).
/// Depends on <see cref="GranitJwtBearerModule"/> for generic JWT Bearer.
/// </summary>
[DependsOn(typeof(GranitJwtBearerModule))]
public sealed class GranitAuthenticationKeycloakModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitKeycloak();
}
