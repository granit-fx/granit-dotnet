using Granit.OpenIddict.Services;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OpenIddictAccountDeletionEtoDispatchJob"/>. Delegates to
/// <see cref="IAccountDeletionEtoReconciler"/> to publish pending erasure events.
/// </summary>
public class OpenIddictAccountDeletionEtoDispatchHandler
{
    public static Task HandleAsync(
        OpenIddictAccountDeletionEtoDispatchJob _,
        IAccountDeletionEtoReconciler reconciler,
        CancellationToken cancellationToken) =>
        reconciler.ReconcileAsync(cancellationToken);
}
