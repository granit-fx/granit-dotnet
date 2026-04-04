using Granit.MultiTenancy;
using Granit.Subscriptions.Events;
using Granit.Subscriptions.Wolverine.Internal;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Creates invoices for Flat/PerSeat plans when a billing cycle completes.
/// Delegates to <see cref="BillingCycleInvoiceOrchestrator"/>.
/// </summary>
[global::Wolverine.Attributes.WolverineHandler]
public static class BillingCycleCompletedHandler
{
    public static async Task HandleAsync(
        BillingCycleCompletedEto eto,
        BillingCycleInvoiceOrchestrator orchestrator,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            await orchestrator.CreateInvoiceAsync(
                eto.SubscriptionId,
                eto.TenantId,
                eto.PlanId,
                eto.PeriodStart,
                eto.PeriodEnd,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
