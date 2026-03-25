using Granit.Authentication.JwtBearer;
using Granit.Authentication.JwtBearer.Keycloak;
using Granit.Authorization;
using Granit.Modularity;
using Granit.Identity;
using Granit.Identity.Endpoints;
using Granit.Identity.Federated.EntityFrameworkCore;
using Granit.Identity.Federated.Keycloak;
using Granit.Persistence.Migrations;

namespace GranitApiFull;

/// <summary>
/// Root module for the application.
/// Bundles (Api, Notifications) are added via the fluent builder in Program.cs.
/// Module-level dependencies are declared here with <c>[DependsOn]</c>.
/// </summary>
[DependsOn(
    typeof(GranitAuthenticationJwtBearerModule),
    typeof(GranitAuthenticationJwtBearerKeycloakModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitIdentityModule),
    typeof(GranitIdentityFederatedKeycloakModule),
    typeof(GranitIdentityFederatedEntityFrameworkCoreModule),
    typeof(GranitIdentityEndpointsModule),
    typeof(GranitPersistenceMigrationsModule))]
public sealed class AppHostModule : GranitModule;
