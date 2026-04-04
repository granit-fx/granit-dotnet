using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Subscriptions.Domain;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Subscriptions.Wolverine.Services;

/// <summary>
/// Creates consolidated invoices (fixed + usage) for PerUnit/Tiered plans.
/// Exclusive invoice creator for plans with usage components.
/// </summary>
public sealed partial class UsageInvoiceOrchestrator(
    ISubscriptionReader subscriptionReader,
    IPlanReader planReader,
    IPricingResolver pricingResolver,
    IMessageBus messageBus,
    ILogger<UsageInvoiceOrchestrator> logger)
{
    public async Task CreateInvoiceAsync(
        Guid tenantId,
        Guid meterDefinitionId,
        string meterName,
        decimal aggregatedValue,
        string unit,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            Log.InvalidEto(logger, "TenantId is empty");
            return;
        }

        if (periodEnd <= periodStart)
        {
            Log.InvalidEto(logger, "PeriodEnd must be after PeriodStart");
            return;
        }

        Subscription? subscription = await subscriptionReader
            .GetActiveForTenantAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            Log.NoActiveSubscription(logger, tenantId);
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

        decimal basePrice = await pricingResolver.ResolveBasePriceAsync(
            subscription.PlanId, subscription.Currency, plan.DefaultInterval,
            subscription.PlanPriceId, cancellationToken)
            .ConfigureAwait(false);

        if (basePrice > 0)
        {
            lineItems.Add(new CreateInvoiceLineItem(
                $"{plan.Name} — {plan.DefaultInterval}",
                1, basePrice,
                InvoiceSourceType.Subscription,
                subscription.Id.ToString()));
        }

        decimal unitPrice = await pricingResolver.ResolveUsageUnitPriceAsync(
            subscription.PlanId, subscription.Currency, plan.DefaultInterval,
            meterDefinitionId.ToString(), subscription.PlanPriceId, cancellationToken)
            .ConfigureAwait(false);

        lineItems.Add(new CreateInvoiceLineItem(
            $"{meterName}: {aggregatedValue} {unit}",
            aggregatedValue, unitPrice,
            InvoiceSourceType.Usage,
            meterDefinitionId.ToString()));

        var command = new CreateInvoiceCommand(
            TenantId: tenantId,
            Currency: subscription.Currency,
            CollectionMethod: CollectionMethod.Auto,
            BillingReason: BillingReason.SubscriptionCycle,
            LineItems: lineItems,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd);

        await messageBus.PublishAsync(command).ConfigureAwait(false);
        Log.UsageInvoiceCreated(logger, tenantId, meterName, aggregatedValue);
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
