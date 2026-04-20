using Granit.MultiTenancy;
using Granit.Subscriptions.Events;

namespace Granit.Subscriptions.Handlers;

/// <summary>
/// Creates invoices for Flat/PerSeat plans when a billing cycle completes.
/// Delegates to <see cref="IBillingCycleInvoiceOrchestrator"/>.
/// </summary>
public class BillingCycleCompletedHandler
{
    public static async Task HandleAsync(
        BillingCycleCompletedEto eto,
        IBillingCycleInvoiceOrchestrator orchestrator,
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
