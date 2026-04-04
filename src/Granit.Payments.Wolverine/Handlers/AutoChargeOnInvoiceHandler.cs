using Granit.Invoicing.Domain;
using Granit.Invoicing.Events;
using Granit.MultiTenancy;
using Granit.Payments.Commands;
using Granit.Payments.Domain;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Payments.Wolverine.Handlers;

/// <summary>
/// Automatically initiates payment when an invoice is finalized with auto-collection.
/// Uses the tenant's default saved payment method to determine the provider and method type.
/// </summary>
internal static partial class AutoChargeOnInvoiceHandler
{
    public static async Task HandleAsync(
        InvoiceFinalizedEto eto,
        IPaymentMethodReader paymentMethodReader,
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

            PaymentMethod? defaultMethod = await paymentMethodReader
                .GetDefaultForTenantAsync(eto.TenantId, cancellationToken)
                .ConfigureAwait(false);

            if (defaultMethod is null)
            {
                Log.NoDefaultPaymentMethod(logger, eto.TenantId, eto.InvoiceId);
                return;
            }

            var command = new InitiatePaymentCommand(
                eto.InvoiceId,
                eto.TenantId,
                eto.Total,
                eto.Currency,
                defaultMethod.Type,
                $"inv-{eto.InvoiceId:N}",
                defaultMethod.ProviderName);

            await messageBus.SendAsync(command).ConfigureAwait(false);
            Log.PaymentInitiated(logger, eto.InvoiceId, defaultMethod.Type, defaultMethod.ProviderName);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Auto-charge initiated for invoice {InvoiceId} via {MethodType} ({ProviderName})")]
        public static partial void PaymentInitiated(ILogger logger, Guid invoiceId, string methodType, string providerName);

        [LoggerMessage(Level = LogLevel.Information, Message = "Skipping auto-charge for invoice {InvoiceId} (manual collection)")]
        public static partial void ManualCollection(ILogger logger, Guid invoiceId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "No default payment method for tenant {TenantId}, cannot auto-charge invoice {InvoiceId}")]
        public static partial void NoDefaultPaymentMethod(ILogger logger, Guid tenantId, Guid invoiceId);
    }
}
