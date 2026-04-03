using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Metering.Events;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Handles <see cref="UsageSummaryReadyEto"/> from Granit.Metering.
/// Combines usage data with the active subscription's fixed charges
/// to create a single invoice via <see cref="CreateInvoiceCommand"/>.
/// </summary>
/// <remarks>
/// This positions Subscriptions as the billing cycle orchestrator (Stripe Billing model):
/// fixed charges (plan price) + usage charges (from Metering) → one invoice.
/// </remarks>
internal static partial class UsageSummaryReadyHandler
{
    public static async Task HandleAsync(
        UsageSummaryReadyEto eto,
        ISubscriptionReader subscriptionReader,
        IMessageBus messageBus,
        ICurrentTenant currentTenant,
        ILogger<UsageSummaryReadyEto> logger,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            // Find the active subscription for this tenant
            Domain.Subscription? subscription = await subscriptionReader
                .GetActiveForTenantAsync(eto.TenantId, cancellationToken)
                .ConfigureAwait(false);

            if (subscription is null)
            {
                Log.NoActiveSubscription(logger, eto.TenantId);
                return;
            }

            // Build usage line item from metering data
            var usageLineItem = new CreateInvoiceLineItem(
                Description: $"{eto.MeterName}: {eto.AggregatedValue} {eto.Unit}",
                Quantity: eto.AggregatedValue,
                UnitPrice: 0m, // Price resolution depends on plan pricing tiers — Phase 2
                SourceType: InvoiceSourceType.Usage,
                SourceId: eto.MeterDefinitionId.ToString());

            // Create invoice command with usage line items
            // In Phase 2, this will also include fixed plan charges
            var command = new CreateInvoiceCommand(
                TenantId: eto.TenantId,
                Currency: "EUR",
                CollectionMethod: CollectionMethod.Auto,
                BillingReason: BillingReason.SubscriptionCycle,
                LineItems: [usageLineItem],
                PeriodStart: eto.PeriodStart,
                PeriodEnd: eto.PeriodEnd);

            await messageBus.SendAsync(command).ConfigureAwait(false);

            Log.UsageInvoiceCreated(logger, eto.TenantId, eto.MeterName, eto.AggregatedValue);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "No active subscription for tenant {TenantId}, skipping usage invoice")]
        public static partial void NoActiveSubscription(ILogger logger, Guid tenantId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Usage invoice created for tenant {TenantId}: {MeterName} = {Value}")]
        public static partial void UsageInvoiceCreated(ILogger logger, Guid tenantId, string meterName, decimal value);
    }
}
