using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Federated.Keycloak.Sync;

namespace Granit.Identity.Federated.Keycloak.BackgroundJobs.Jobs;

/// <summary>
/// Wolverine handler for <see cref="KeycloakClientRoleSyncJob"/>. Delegates straight
/// to <c>KeycloakClientRoleSyncService.SyncAsync</c> — the same code path invoked at
/// host boot by <c>KeycloakClientRoleSyncContributor</c>.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class KeycloakClientRoleSyncHandler
{
    public static Task HandleAsync(
        KeycloakClientRoleSyncJob _,
        KeycloakClientRoleSyncService service,
        CancellationToken cancellationToken) =>
        service.SyncAsync(cancellationToken);
}
