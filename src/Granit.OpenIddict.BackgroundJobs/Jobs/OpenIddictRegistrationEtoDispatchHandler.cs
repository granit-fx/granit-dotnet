using Granit.OpenIddict.Services;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OpenIddictRegistrationEtoDispatchJob"/>. Delegates to
/// <see cref="IRegistrationEtoReconciler"/> to publish pending registration events.
/// </summary>
public class OpenIddictRegistrationEtoDispatchHandler
{
    public static Task HandleAsync(
        OpenIddictRegistrationEtoDispatchJob _,
        IRegistrationEtoReconciler reconciler,
        CancellationToken cancellationToken) =>
        reconciler.ReconcileAsync(cancellationToken);
}
