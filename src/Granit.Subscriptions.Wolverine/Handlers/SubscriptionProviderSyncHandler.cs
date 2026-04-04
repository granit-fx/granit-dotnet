using Granit.MultiTenancy;
using Granit.Subscriptions.Events;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Syncs subscription state to the external provider after FSM transitions.
/// Delegates to <see cref="ISubscriptionProviderSyncService"/>.
/// </summary>
[global::Wolverine.Attributes.WolverineHandler]
public static class SubscriptionProviderSyncHandler
{
    public static async Task HandleAsync(
        SubscriptionCancelledEto eto,
        ISubscriptionProviderSyncService syncService,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            await syncService.SyncCancellationAsync(
                eto.SubscriptionId,
                eto.TenantId,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
