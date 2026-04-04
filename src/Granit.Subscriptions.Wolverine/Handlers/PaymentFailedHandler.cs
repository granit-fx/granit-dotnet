using Granit.MultiTenancy;
using Granit.Payments.Events;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Dunning entry point. Delegates to <see cref="IDunningService"/> for payment failure processing.
/// </summary>
internal static class PaymentFailedHandler
{
    public static async Task HandleAsync(
        PaymentFailedEto eto,
        IDunningService dunningService,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            await dunningService.HandlePaymentFailureAsync(
                eto.TenantId,
                eto.InvoiceId,
                eto.Amount,
                eto.Currency,
                eto.MethodType,
                eto.ProviderName,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
