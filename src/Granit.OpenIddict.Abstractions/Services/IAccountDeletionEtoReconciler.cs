namespace Granit.OpenIddict.Services;

/// <summary>
/// Publishes the GDPR Art. 17 erasure event for soft-deleted users whose event has not yet been
/// dispatched. Driven by a recurring background job so the event survives a crash between the
/// soft-delete commit and the publish.
/// </summary>
public interface IAccountDeletionEtoReconciler
{
    /// <summary>Publishes pending erasure events and marks them dispatched.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReconcileAsync(CancellationToken cancellationToken);
}
