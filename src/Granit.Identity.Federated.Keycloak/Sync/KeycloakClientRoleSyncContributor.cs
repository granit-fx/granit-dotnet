using Granit.Persistence.DataSeeding;

namespace Granit.Identity.Federated.Keycloak.Sync;

/// <summary>
/// Host-level data seed contributor that triggers
/// <see cref="KeycloakClientRoleSyncService.SyncAsync(CancellationToken)"/> at boot.
/// </summary>
/// <remarks>
/// Registered as transient by <c>AddGranitIdentityKeycloak</c>. The enclosing seeder
/// runs all <see cref="IHostDataSeedContributor"/> contributors once per host boot in a
/// tenantless scope — which matches the contract required by this sync (client roles are
/// host-scope, persisted with <c>TenantId = null</c> and <c>ClientId = the Keycloak clientId</c>).
/// </remarks>
internal sealed class KeycloakClientRoleSyncContributor(
    KeycloakClientRoleSyncService syncService) : IHostDataSeedContributor
{
    public Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default) =>
        syncService.SyncAsync(cancellationToken);
}
