using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Federated.EntraId.Sync;

namespace Granit.Identity.Federated.EntraId.BackgroundJobs.Jobs;

/// <summary>
/// Wolverine handler for <see cref="EntraIdClientRoleSyncJob"/>. Delegates to
/// <c>EntraIdClientRoleSyncService.SyncAsync</c> — the same code path invoked at
/// host boot by <c>EntraIdClientRoleSyncContributor</c>.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class EntraIdClientRoleSyncHandler
{
    public static Task HandleAsync(
        EntraIdClientRoleSyncJob _,
        EntraIdClientRoleSyncService service,
        CancellationToken cancellationToken) =>
        service.SyncAsync(cancellationToken);
}
