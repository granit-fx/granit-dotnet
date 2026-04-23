using Granit.Identity.Federated.Cognito.Sync;

namespace Granit.Identity.Federated.Cognito.BackgroundJobs.Jobs;

/// <summary>
/// Wolverine handler for <see cref="CognitoClientRoleSyncJob"/>. Delegates to
/// <c>CognitoClientRoleSyncService.SyncAsync</c> — the same code path invoked at
/// host boot by <c>CognitoClientRoleSyncContributor</c>.
/// </summary>
public class CognitoClientRoleSyncHandler
{
    public static Task HandleAsync(
        CognitoClientRoleSyncJob _,
        CognitoClientRoleSyncService service,
        CancellationToken cancellationToken) =>
        service.SyncAsync(cancellationToken);
}
