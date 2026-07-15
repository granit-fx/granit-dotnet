using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Federated.Sync;

namespace Granit.Identity.Federated.BackgroundJobs.Jobs;

/// <summary>
/// Wolverine handler for <see cref="ClientRoleSyncJob"/>. Drives every registered provider's
/// <see cref="IClientRoleSyncPolicy"/> through the shared <see cref="ClientRoleSyncEngine"/> — the
/// same code path invoked at host boot by <c>ClientRoleSyncContributor</c>.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class ClientRoleSyncHandler
{
    public static async Task HandleAsync(
        ClientRoleSyncJob _,
        IClientRoleSyncEngine engine,
        IEnumerable<IClientRoleSyncPolicy> policies,
        CancellationToken cancellationToken)
    {
        foreach (IClientRoleSyncPolicy policy in policies)
        {
            await engine.SyncAsync(policy, cancellationToken).ConfigureAwait(false);
        }
    }
}
