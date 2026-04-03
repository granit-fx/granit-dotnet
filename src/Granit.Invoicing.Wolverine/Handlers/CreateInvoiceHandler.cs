using Granit.Guids;
using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Granit.Invoicing.Wolverine.Handlers;

/// <summary>
/// Handles <see cref="CreateInvoiceCommand"/> — creates a draft invoice with line items.
/// Any module (Subscriptions, Commerce, admin) can send this command.
/// </summary>
internal static partial class CreateInvoiceHandler
{
    public static async Task HandleAsync(
        CreateInvoiceCommand command,
        IInvoiceWriter invoiceWriter,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        ILogger<CreateInvoiceCommand> logger,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(command.TenantId))
        {
            var invoice = Invoice.Create(
                guidGenerator.Create(),
                command.TenantId,
                InvoiceDocumentType.Invoice,
                command.Currency,
                command.CollectionMethod,
                command.BillingReason,
                periodStart: command.PeriodStart,
                periodEnd: command.PeriodEnd);

            foreach (CreateInvoiceLineItem lineItem in command.LineItems)
            {
                invoice.AddLineItem(InvoiceLineItem.Create(
                    guidGenerator.Create(),
                    lineItem.Description,
                    lineItem.Quantity,
                    lineItem.UnitPrice,
                    lineItem.SourceType,
                    lineItem.SourceId));
            }

            await invoiceWriter.AddAsync(invoice, cancellationToken).ConfigureAwait(false);
            Log.InvoiceCreated(logger, invoice.Id, command.TenantId);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} created for tenant {TenantId}")]
        public static partial void InvoiceCreated(ILogger logger, Guid invoiceId, Guid tenantId);
    }
}
