using Granit.Guids;
using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Dtos;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Invoicing.Wolverine.Handlers;

/// <summary>
/// Handles <see cref="CreateInvoiceCommand"/> — creates a draft invoice, calculates tax,
/// generates a document number, and finalizes it.
/// </summary>
/// <remarks>
/// Any module (Subscriptions, Commerce, admin) can send this command.
/// Tax calculation and number generation are optional — if no provider is registered,
/// the invoice is created without tax and without finalization.
/// </remarks>
internal static partial class CreateInvoiceHandler
{
    public static async Task HandleAsync(
        CreateInvoiceCommand command,
        IInvoiceWriter invoiceWriter,
        IGuidGenerator guidGenerator,
        IClock clock,
        ICurrentTenant currentTenant,
        ITaxCalculator? taxCalculator,
        IInvoiceNumberGenerator? numberGenerator,
        ILogger<CreateInvoiceCommand> logger,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(command.TenantId))
        {
            // 1. Create draft invoice
            var invoice = Invoice.Create(
                guidGenerator.Create(),
                command.TenantId,
                InvoiceDocumentType.Invoice,
                command.Currency,
                command.CollectionMethod,
                command.BillingReason,
                periodStart: command.PeriodStart,
                periodEnd: command.PeriodEnd);

            // 2. Add line items
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

            // 3. Calculate tax (if provider is registered)
            if (taxCalculator is not null && invoice.BillingAddress is not null)
            {
                var taxRequest = new TaxRequest(
                    LineItems: invoice.LineItems.Select(li => new TaxLineItem(
                        li.Description,
                        li.Quantity * li.UnitPrice,
                        TaxCode: null)).ToList(),
                    SellerAddress: invoice.BillingAddress,
                    BuyerAddress: invoice.BillingAddress);

                TaxResult taxResult = await taxCalculator
                    .CalculateAsync(taxRequest, cancellationToken)
                    .ConfigureAwait(false);

                invoice.SetTaxTotal(taxResult.TotalTax);
                Log.TaxCalculated(logger, invoice.Id, taxResult.TotalTax, taxCalculator.Name);
            }

            // 4. Generate number and finalize (if number generator is registered)
            if (numberGenerator is not null)
            {
                string documentNumber = await numberGenerator
                    .GenerateNextAsync(InvoiceDocumentType.Invoice, command.TenantId, cancellationToken)
                    .ConfigureAwait(false);

                DateTimeOffset now = clock.Now;
                invoice.Finalize(documentNumber, now, dueAt: now.AddDays(30));
                Log.InvoiceFinalized(logger, invoice.Id, documentNumber);
            }

            await invoiceWriter.AddAsync(invoice, cancellationToken).ConfigureAwait(false);
            Log.InvoiceCreated(logger, invoice.Id, command.TenantId);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} created for tenant {TenantId}")]
        public static partial void InvoiceCreated(ILogger logger, Guid invoiceId, Guid tenantId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Tax calculated for invoice {InvoiceId}: {TaxTotal} via {Provider}")]
        public static partial void TaxCalculated(ILogger logger, Guid invoiceId, decimal taxTotal, string provider);

        [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} finalized as {DocumentNumber}")]
        public static partial void InvoiceFinalized(ILogger logger, Guid invoiceId, string documentNumber);
    }
}
