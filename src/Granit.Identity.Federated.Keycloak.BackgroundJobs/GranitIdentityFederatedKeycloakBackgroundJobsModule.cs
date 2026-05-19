using Granit.BackgroundJobs;
using Granit.Modularity;

namespace Granit.Identity.Federated.Keycloak.BackgroundJobs;

/// <summary>
/// Registers the recurring Keycloak client-role sync job. The handler reuses the
/// Phase 2 <c>KeycloakClientRoleSyncService</c> registered by
/// <c>AddGranitIdentityKeycloak</c> — no additional DI wiring required here.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitIdentityFederatedKeycloakModule))]
public sealed class GranitIdentityFederatedKeycloakBackgroundJobsModule : GranitModule;
