using Granit.Invoicing;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Events;
using Granit.MultiTenancy;
using Granit.Payments.Commands;
using Granit.Payments.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Payments.Internal;

/// <summary>
/// Automatically initiates payment when an invoice is finalized with auto-collection.
/// Delegates to <see cref="IInvoicePrePaymentProcessor"/> before charging — the default
/// pass-through returns the full total, but <c>Granit.CustomerBalance.Wolverine</c> can
/// replace it to deduct available credit first.
/// </summary>
internal sealed partial class DefaultAutoChargeService(
    IInvoicePrePaymentProcessor prePaymentProcessor,
    IPaymentMethodReader paymentMethodReader,
    IPaymentCommandDispatcher paymentCommandDispatcher,
    ICurrentTenant currentTenant,
    ILogger<DefaultAutoChargeService> logger) : IAutoChargeService
{
    public async Task HandleAsync(InvoiceFinalizedEto eto, CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            PrePaymentResult result = await prePaymentProcessor
                .ProcessAsync(eto, cancellationToken).ConfigureAwait(false);

            if (eto.CollectionMethod != CollectionMethod.Auto)
            {
                Log.ManualCollection(logger, eto.InvoiceId);
                return;
            }

            if (result.RemainingAmount <= 0)
            {
                Log.FullyCoveredByCredit(logger, eto.InvoiceId);
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
                result.RemainingAmount,
                eto.Currency,
                defaultMethod.Type,
                $"inv-{eto.InvoiceId:N}",
                defaultMethod.ProviderName);

            await paymentCommandDispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
            Log.PaymentInitiated(logger, eto.InvoiceId, result.RemainingAmount, defaultMethod.Type, defaultMethod.ProviderName);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Auto-charge initiated for invoice {InvoiceId}: {Amount} via {MethodType} ({ProviderName})")]
        public static partial void PaymentInitiated(ILogger logger, Guid invoiceId, decimal amount, string methodType, string providerName);

        [LoggerMessage(Level = LogLevel.Information, Message = "Skipping auto-charge for invoice {InvoiceId} (manual collection)")]
        public static partial void ManualCollection(ILogger logger, Guid invoiceId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} fully covered by credit balance, no PSP charge needed")]
        public static partial void FullyCoveredByCredit(ILogger logger, Guid invoiceId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "No default payment method for tenant {TenantId}, cannot auto-charge invoice {InvoiceId}")]
        public static partial void NoDefaultPaymentMethod(ILogger logger, Guid tenantId, Guid invoiceId);
    }
}
