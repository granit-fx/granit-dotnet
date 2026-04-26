using Granit.Contacts;
using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;
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
    IDefaultContactResolver defaultContactResolver,
    ILogger<DefaultInvoiceCreationService> logger,
    ITaxCalculator? taxCalculator = null,
    IInvoiceNumberGenerator? numberGenerator = null) : IInvoiceCreationService
{
    public async Task CreateAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        ContactId contactId = await ResolveContactIdAsync(command, cancellationToken).ConfigureAwait(false);

        var invoice = Invoice.Create(
            guidGenerator.Create(),
            command.TenantId,
            contactId,
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
                new LineItemSource(lineItem.SourceType, lineItem.SourceId),
                productId: lineItem.ProductId));
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

    private async Task<ContactId> ResolveContactIdAsync(CreateInvoiceCommand command, CancellationToken cancellationToken)
    {
        if (command.ContactId is { } explicitId)
        {
            return ContactId.Create(explicitId);
        }

        Contact? defaultContact = await defaultContactResolver
            .GetDefaultForTenantAsync(command.TenantId, cancellationToken)
            .ConfigureAwait(false);

        return defaultContact is not null
            ? ContactId.Create(defaultContact.Id)
            : throw new InvalidOperationException(
                $"No default Contact resolved for tenant '{command.TenantId}'. Either pass " +
                $"{nameof(CreateInvoiceCommand)}.{nameof(CreateInvoiceCommand.ContactId)} explicitly or " +
                $"seed a host-scoped Contact with provider '{ContactExternalProviderNames.Tenant}' = " +
                $"'{command.TenantId}' (typically done at tenant-provisioning time).");
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
