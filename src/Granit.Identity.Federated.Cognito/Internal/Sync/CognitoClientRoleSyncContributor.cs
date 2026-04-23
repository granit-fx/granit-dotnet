using Granit.Persistence.EntityFrameworkCore.DataSeeding;

namespace Granit.Identity.Federated.Cognito.Internal.Sync;

/// <summary>
/// Host-level data seed contributor that triggers
/// <see cref="CognitoClientRoleSyncService.SyncAsync(CancellationToken)"/> at boot.
/// Mirrors <c>KeycloakClientRoleSyncContributor</c> and <c>EntraIdClientRoleSyncContributor</c>
/// (ADR-025 / ADR-026 / ADR-027).
/// </summary>
internal sealed class CognitoClientRoleSyncContributor(
    CognitoClientRoleSyncService syncService) : IHostDataSeedContributor
{
    public Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default) =>
        syncService.SyncAsync(cancellationToken);
}
