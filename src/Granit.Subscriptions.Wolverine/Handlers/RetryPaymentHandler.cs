using Granit.MultiTenancy;
using Granit.Subscriptions.Scheduling;
using Granit.Subscriptions.Wolverine.Services;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Handles scheduled payment retries during dunning.
/// Delegates to <see cref="PaymentRetryDispatcher"/>.
/// </summary>
public class RetryPaymentHandler
{
    public static async Task HandleAsync(
        RetryPaymentPayload payload,
        PaymentRetryDispatcher dispatcher,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(payload.TenantId))
        {
            await dispatcher.DispatchRetryAsync(payload).ConfigureAwait(false);
        }
    }
}
