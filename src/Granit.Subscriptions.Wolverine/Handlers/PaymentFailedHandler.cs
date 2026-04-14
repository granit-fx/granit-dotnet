using Granit.MultiTenancy;
using Granit.Payments.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Dunning entry point. Delegates to <see cref="IDunningService"/> for payment failure processing.
/// </summary>
public partial class PaymentFailedHandler
{
    public static async Task HandleAsync(
        PaymentFailedEto eto,
        IDunningService dunningService,
        ICurrentTenant currentTenant,
        ILogger<PaymentFailedHandler> logger,
        CancellationToken cancellationToken)
    {
        try
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
        catch (DbUpdateConcurrencyException ex)
        {
            Log.ConcurrencyConflictSkipped(logger, eto.InvoiceId, ex);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Payment failure handler skipped due to concurrency conflict on invoice {InvoiceId}; a concurrent update already modified the subscription.")]
        public static partial void ConcurrencyConflictSkipped(ILogger logger, Guid invoiceId, Exception exception);
    }
}
