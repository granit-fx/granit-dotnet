using Granit.Guids;
using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Granit.Invoicing.Dtos;
using Granit.Parties;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Invoicing.Internal;

internal sealed partial class DefaultInvoiceCreationService(
    IInvoiceWriter invoiceWriter,
    IPartyReader contactReader,
    IGuidGenerator guidGenerator,
    IClock clock,
    IDefaultPartyResolver defaultContactResolver,
    ILogger<DefaultInvoiceCreationService> logger,
    ITaxCalculator? taxCalculator = null,
    IInvoiceNumberGenerator? numberGenerator = null) : IInvoiceCreationService
{
    public async Task CreateAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        Party contact = await ResolveContactAsync(command, cancellationToken).ConfigureAwait(false);
        var contactId = PartyId.Create(contact.Id);

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

        Granit.Parties.Domain.BillingAddress? billingSnapshot = contact.GetBillingAddressSnapshot();

        if (taxCalculator is not null && billingSnapshot is not null)
        {
            var taxRequest = new TaxRequest(
                LineItems: invoice.LineItems.Select(li => new TaxLineItem(
                    li.Description,
                    li.Quantity * li.UnitPrice,
                    TaxCode: null)).ToList(),
                SellerAddress: billingSnapshot,
                BuyerAddress: billingSnapshot,
                BuyerContactId: contact.Id);

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
            invoice.Finalize(documentNumber, now, dueAt: now.AddDays(30), billingAddressSnapshot: billingSnapshot);
            Log.InvoiceFinalized(logger, invoice.Id, documentNumber);
        }

        await invoiceWriter.AddAsync(invoice, cancellationToken).ConfigureAwait(false);
        Log.InvoiceCreated(logger, invoice.Id, command.TenantId);
    }

    private async Task<Party> ResolveContactAsync(CreateInvoiceCommand command, CancellationToken cancellationToken)
    {
        if (command.PartyId is { } explicitId)
        {
            Party? explicitContact = await contactReader
                .GetByIdAsync(PartyId.Create(explicitId), cancellationToken)
                .ConfigureAwait(false);
            return explicitContact ?? throw new InvalidOperationException(
                $"Party '{explicitId}' referenced by {nameof(CreateInvoiceCommand)}.{nameof(CreateInvoiceCommand.PartyId)} was not found.");
        }

        Party? defaultContact = await defaultContactResolver
            .GetDefaultForTenantAsync(command.TenantId, cancellationToken)
            .ConfigureAwait(false);

        return defaultContact ?? throw new InvalidOperationException(
            $"No default Party resolved for tenant '{command.TenantId}'. Either pass " +
            $"{nameof(CreateInvoiceCommand)}.{nameof(CreateInvoiceCommand.PartyId)} explicitly or " +
            $"seed a host-scoped Party with provider '{PartyExternalProviderNames.Tenant}' = " +
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
