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
        using (currentTenant.Change(eto.TenantId))
        {
            try
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
            catch (DbUpdateConcurrencyException)
            {
                Log.ConcurrentDunningSkipped(logger, eto.TenantId);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Concurrent dunning attempt skipped for tenant {TenantId}")]
        public static partial void ConcurrentDunningSkipped(ILogger logger, Guid tenantId);
    }
}
