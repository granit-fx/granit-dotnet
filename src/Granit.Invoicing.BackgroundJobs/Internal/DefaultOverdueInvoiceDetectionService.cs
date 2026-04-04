using Granit.Invoicing;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Events;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Invoicing.BackgroundJobs.Internal;

internal sealed partial class DefaultOverdueInvoiceDetectionService(
    IInvoiceReader invoiceReader,
    IMessageBus messageBus,
    IClock clock,
    ICurrentTenant currentTenant,
    ILogger<DefaultOverdueInvoiceDetectionService> logger) : IOverdueInvoiceDetectionService
{
    public async Task DetectAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Invoice> overdueInvoices = await invoiceReader
            .GetOverdueAsync(clock.Now, cancellationToken)
            .ConfigureAwait(false);

        foreach (Invoice invoice in overdueInvoices)
        {
            using (currentTenant.Change(invoice.TenantId))
            {
                await messageBus.PublishAsync(
                    new InvoiceOverdueEto(invoice.Id, invoice.TenantId!.Value, invoice.DueAt!.Value))
                    .ConfigureAwait(false);

                Log.InvoiceOverdue(logger, invoice.Id, invoice.InvoiceNumber ?? "N/A");
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Invoice {InvoiceId} ({InvoiceNumber}) is overdue")]
        public static partial void InvoiceOverdue(ILogger logger, Guid invoiceId, string invoiceNumber);
    }
}
