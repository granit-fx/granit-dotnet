using Granit.Invoicing.Events;
using Granit.MultiTenancy;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Reactivates a PastDue subscription when its invoice is paid.
/// Delegates to <see cref="ISubscriptionReactivationService"/>.
/// </summary>
internal static class InvoicePaidHandler
{
    public static async Task HandleAsync(
        InvoicePaidEto eto,
        ISubscriptionReactivationService reactivationService,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            await reactivationService.TryReactivateAsync(eto.TenantId, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
