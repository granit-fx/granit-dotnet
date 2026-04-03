using Granit.Invoicing.Domain;
using Granit.Invoicing.Events;
using Granit.Payments.Commands;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Payments.Wolverine.Handlers;

/// <summary>
/// Handles InvoiceFinalizedEto from Invoicing module.
/// If CollectionMethod is Auto, initiates payment via InitiatePaymentCommand.
/// </summary>
internal static partial class InvoiceFinalizedHandler
{
    public static async Task HandleAsync(
        InvoiceFinalizedEto eto,
        IMessageBus messageBus,
        ILogger<InvoiceFinalizedEto> logger,
        CancellationToken cancellationToken)
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
            Domain.PaymentMethodType.Card,
            $"inv-{eto.InvoiceId}");

        await messageBus.PublishAsync(command).ConfigureAwait(false);
        Log.PaymentInitiated(logger, eto.InvoiceId, eto.Total, eto.Currency);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Payment initiated for invoice {InvoiceId}: {Amount} {Currency}")]
        public static partial void PaymentInitiated(ILogger logger, Guid invoiceId, decimal amount, string currency);

        [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} uses manual collection, skipping auto-charge")]
        public static partial void ManualCollection(ILogger logger, Guid invoiceId);
    }
}
