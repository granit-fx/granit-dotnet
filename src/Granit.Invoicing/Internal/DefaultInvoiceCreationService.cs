using Granit.Guids;
using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Granit.Invoicing.Dtos;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Invoicing.Internal;

internal sealed partial class DefaultInvoiceCreationService(
    IInvoiceWriter invoiceWriter,
    IGuidGenerator guidGenerator,
    IClock clock,
    ILogger<DefaultInvoiceCreationService> logger,
    ITaxCalculator? taxCalculator = null,
    IInvoiceNumberGenerator? numberGenerator = null) : IInvoiceCreationService
{
    public async Task CreateAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        var invoice = Invoice.Create(
            guidGenerator.Create(),
            command.TenantId,
            InvoiceDocumentType.Invoice,
            command.Currency,
            command.CollectionMethod,
            command.BillingReason,
            period: command.PeriodStart.HasValue && command.PeriodEnd.HasValue
                ? new BillingPeriod(command.PeriodStart.Value, command.PeriodEnd.Value)
                : null);

        foreach (CreateInvoiceLineItem lineItem in command.LineItems)
        {
            invoice.AddLineItem(InvoiceLineItem.Create(
                guidGenerator.Create(),
                lineItem.Description,
                lineItem.Quantity,
                lineItem.UnitPrice,
                new LineItemSource(lineItem.SourceType, lineItem.SourceId)));
        }

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
