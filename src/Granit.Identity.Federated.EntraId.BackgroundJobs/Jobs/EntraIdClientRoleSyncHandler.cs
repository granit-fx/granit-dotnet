using Granit.Identity.Federated.EntraId.Sync;

namespace Granit.Identity.Federated.EntraId.BackgroundJobs.Jobs;

/// <summary>
/// Wolverine handler for <see cref="EntraIdClientRoleSyncJob"/>. Delegates to
/// <c>EntraIdClientRoleSyncService.SyncAsync</c> — the same code path invoked at
/// host boot by <c>EntraIdClientRoleSyncContributor</c>.
/// </summary>
public class EntraIdClientRoleSyncHandler
{
    public static Task HandleAsync(
        EntraIdClientRoleSyncJob _,
        EntraIdClientRoleSyncService service,
        CancellationToken cancellationToken) =>
        service.SyncAsync(cancellationToken);
}
