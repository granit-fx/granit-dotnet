using Granit.Persistence.EntityFrameworkCore.DataSeeding;

namespace Granit.Identity.Federated.EntraId.Sync;

/// <summary>
/// Host-level data seed contributor that triggers
/// <see cref="EntraIdClientRoleSyncService.SyncAsync(CancellationToken)"/> at boot.
/// Mirrors <c>KeycloakClientRoleSyncContributor</c> (ADR-025).
/// </summary>
internal sealed class EntraIdClientRoleSyncContributor(
    EntraIdClientRoleSyncService syncService) : IHostDataSeedContributor
{
    public Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default) =>
        syncService.SyncAsync(cancellationToken);
}
