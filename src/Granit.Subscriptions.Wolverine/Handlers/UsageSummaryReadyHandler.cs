using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Metering.Events;
using Granit.MultiTenancy;
using Granit.Subscriptions.Domain;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Creates consolidated invoices (fixed + usage) for PerUnit/Tiered plans.
/// This is the exclusive invoice creator for plans with usage components —
/// <see cref="BillingCycleCompletedHandler"/> skips these plans to prevent split-brain.
/// </summary>
internal static partial class UsageSummaryReadyHandler
{
    public static async Task HandleAsync(
        UsageSummaryReadyEto eto,
        ISubscriptionReader subscriptionReader,
        IPlanReader planReader,
        IPricingResolver pricingResolver,
        IMessageBus messageBus,
        ICurrentTenant currentTenant,
        ILogger<UsageSummaryReadyEto> logger,
        CancellationToken cancellationToken)
    {
        if (eto.TenantId == Guid.Empty)
        {
            Log.InvalidEto(logger, "TenantId is empty");
            return;
        }

        if (eto.PeriodEnd <= eto.PeriodStart)
        {
            Log.InvalidEto(logger, "PeriodEnd must be after PeriodStart");
            return;
        }

        using (currentTenant.Change(eto.TenantId))
        {
            Subscription? subscription = await subscriptionReader
                .GetActiveForTenantAsync(eto.TenantId, cancellationToken)
                .ConfigureAwait(false);

            if (subscription is null)
            {
                Log.NoActiveSubscription(logger, eto.TenantId);
                return;
            }

            Plan? plan = await planReader
                .GetByIdAsync(subscription.PlanId, cancellationToken)
                .ConfigureAwait(false);

            if (plan is null)
            {
                Log.PlanNotFound(logger, subscription.PlanId);
                return;
            }

            var lineItems = new List<CreateInvoiceLineItem>();

            // Fixed plan charge (base price)
            decimal basePrice = await pricingResolver.ResolveBasePriceAsync(
                subscription.PlanId, subscription.Currency, plan.DefaultInterval, cancellationToken)
                .ConfigureAwait(false);

            if (basePrice > 0)
            {
                lineItems.Add(new CreateInvoiceLineItem(
                    $"{plan.Name} — {plan.DefaultInterval}",
                    1, basePrice,
                    InvoiceSourceType.Subscription,
                    subscription.Id.ToString()));
            }

            // Usage charge
            decimal unitPrice = await pricingResolver.ResolveUsageUnitPriceAsync(
                subscription.PlanId, subscription.Currency, plan.DefaultInterval,
                eto.MeterDefinitionId.ToString(), cancellationToken)
                .ConfigureAwait(false);

            lineItems.Add(new CreateInvoiceLineItem(
                $"{eto.MeterName}: {eto.AggregatedValue} {eto.Unit}",
                eto.AggregatedValue, unitPrice,
                InvoiceSourceType.Usage,
                eto.MeterDefinitionId.ToString()));

            var command = new CreateInvoiceCommand(
                TenantId: eto.TenantId,
                Currency: subscription.Currency,
                CollectionMethod: CollectionMethod.Auto,
                BillingReason: BillingReason.SubscriptionCycle,
                LineItems: lineItems,
                PeriodStart: eto.PeriodStart,
                PeriodEnd: eto.PeriodEnd);

            await messageBus.PublishAsync(command).ConfigureAwait(false);
            Log.UsageInvoiceCreated(logger, eto.TenantId, eto.MeterName, eto.AggregatedValue);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "No active subscription for tenant {TenantId}, skipping usage invoice")]
        public static partial void NoActiveSubscription(ILogger logger, Guid tenantId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Usage invoice created for tenant {TenantId}: {MeterName} = {Value}")]
        public static partial void UsageInvoiceCreated(ILogger logger, Guid tenantId, string meterName, decimal value);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid UsageSummaryReadyEto received: {Reason}")]
        public static partial void InvalidEto(ILogger logger, string reason);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Plan {PlanId} not found for usage invoice")]
        public static partial void PlanNotFound(ILogger logger, Guid planId);
    }
}
