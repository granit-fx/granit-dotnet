using Granit.Invoicing.Domain;
using Granit.Invoicing.Events;
using Granit.MultiTenancy;
using Granit.Payments.Commands;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Payments.Wolverine.Handlers;

/// <summary>
/// Automatically initiates payment when an invoice is finalized with auto-collection.
/// Consumes <see cref="InvoiceFinalizedEto"/> and sends <see cref="InitiatePaymentCommand"/>.
/// </summary>
/// <remarks>
/// The idempotency key is derived from the invoice ID to prevent double-charging
/// the same invoice on Wolverine retries. For payment retry after failure (dunning),
/// a new command with a different key is created by the dunning handler.
/// </remarks>
internal static partial class AutoChargeOnInvoiceHandler
{
    public static async Task HandleAsync(
        InvoiceFinalizedEto eto,
        IMessageBus messageBus,
        ICurrentTenant currentTenant,
        ILogger<InvoiceFinalizedEto> logger,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            if (eto.CollectionMethod != CollectionMethod.Auto)
            {
                Log.ManualCollection(logger, eto.InvoiceId);
                return;
            }

            var command = new InitiatePaymentCommand(
                eto.InvoiceId,
                eto.TenantId,
                eto.Total,
                eto.Currency,
                Domain.PaymentMethods.Card,
                $"inv-{eto.InvoiceId:N}");

            await messageBus.SendAsync(command).ConfigureAwait(false);
            Log.PaymentInitiated(logger, eto.InvoiceId);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Auto-charge initiated for finalized invoice {InvoiceId}")]
        public static partial void PaymentInitiated(ILogger logger, Guid invoiceId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Skipping auto-charge for invoice {InvoiceId} (manual collection)")]
        public static partial void ManualCollection(ILogger logger, Guid invoiceId);
    }
}
