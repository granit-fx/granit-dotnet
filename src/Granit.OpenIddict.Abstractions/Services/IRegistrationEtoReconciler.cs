namespace Granit.OpenIddict.Services;

/// <summary>
/// Publishes the <c>UserRegisteredEto</c> for accounts whose registration event has not yet been
/// dispatched. Driven by a recurring background job so the registration side effects survive a crash
/// between the account creation and the inline publish.
/// </summary>
public interface IRegistrationEtoReconciler
{
    /// <summary>Publishes pending registration events and marks them dispatched.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReconcileAsync(CancellationToken cancellationToken);
}
