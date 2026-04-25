using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Federated.Cognito.Sync;

namespace Granit.Identity.Federated.Cognito.BackgroundJobs.Jobs;

/// <summary>
/// Wolverine handler for <see cref="CognitoClientRoleSyncJob"/>. Delegates to
/// <c>CognitoClientRoleSyncService.SyncAsync</c> — the same code path invoked at
/// host boot by <c>CognitoClientRoleSyncContributor</c>.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class CognitoClientRoleSyncHandler
{
    public static Task HandleAsync(
        CognitoClientRoleSyncJob _,
        CognitoClientRoleSyncService service,
        CancellationToken cancellationToken) =>
        service.SyncAsync(cancellationToken);
}
